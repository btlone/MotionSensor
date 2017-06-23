using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Devices.Gpio;
using Windows.Foundation.Collections;
using MotionSensor.Enums;

namespace MotionSensor
{ 
    public sealed partial class MainPage
    {
        private readonly int[] hackLedID = { 5, 6, 13 };
        private Dictionary<LedID, GpioPin> leds;
        private Dictionary<MotionDetectorID, GpioPin> motionDetectors;
        private readonly PassCounterManager passCounterManager;
        private AppServiceConnection appServiceConnection;

        public MainPage()
        {
            InitializeComponent();
            InitializeGPIO();
            passCounterManager = new PassCounterManager();
            passCounterManager.CounterChanged += OnCounterChanged;

            InitializeAppServiceConnection();
            RunAppServiceConnection();
        }
        
        private void InitializeAppServiceConnection()
        {
            appServiceConnection = new AppServiceConnection
            {
                PackageFamilyName = Package.Current.Id.FamilyName,
                AppServiceName = Package.Current.DisplayName
            };
        }

        private async void RunAppServiceConnection()
        {
            // Send a initialize request 
            var res = await appServiceConnection.OpenAsync();
            if (res == AppServiceConnectionStatus.Success)
            {
                var message = new ValueSet();
                message.Add("Command", "Initialize");
                var response = await appServiceConnection.SendMessageAsync(message);
                if (response.Status != AppServiceResponseStatus.Success)
                {
                    throw new Exception("Failed to send message");
                }
                appServiceConnection.RequestReceived += OnAppServiceConnectionMessageReceived;
            }
        }

        private void OnAppServiceConnectionMessageReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var message = args.Request.Message;
            string newState = message["State"] as string;
            var stringArray = newState?.Split(' ');

            if (stringArray != null)
                foreach (var item in stringArray)
                {
                    LedID? ledid = null;

                    switch (item)
                    {
                        case "Red":
                            ledid = LedID.LED_1;
                            break;
                        case "Yellow":
                            ledid = LedID.LED_2;
                            break;
                        case "Green":
                            ledid = LedID.LED_3;
                            break;
                        default:
                            break;
                    }

                    if (ledid.HasValue)
                        FlipLed(leds[ledid.Value]);
                }
        }

        private void InitializeGPIO()
        {
            var gpio = GpioController.GetDefault();

            leds = new Dictionary<LedID, GpioPin>
            {
                { LedID.LED_1, gpio.OpenPin((int) LedID.LED_1) },
                { LedID.LED_2, gpio.OpenPin((int) LedID.LED_2) },
                { LedID.LED_3, gpio.OpenPin((int) LedID.LED_3) },
            };

            foreach (var item in leds.Values)
            {
                item.SetDriveMode(GpioPinDriveMode.Output);
                item.Write(GpioPinValue.High);
            }

            motionDetectors = new Dictionary<MotionDetectorID, GpioPin>()
            {
                { MotionDetectorID.Device_1, gpio.OpenPin((int) MotionDetectorID.Device_1) },
                { MotionDetectorID.Device_2, gpio.OpenPin((int) MotionDetectorID.Device_2) },
            };

            foreach (var item in motionDetectors.Values)
            {
                item.SetDriveMode(GpioPinDriveMode.Input);
                item.DebounceTimeout = new TimeSpan(0, 0, 0, 0, 25);
                item.ValueChanged += OnPinPIRValueChanged;
            }
        }

        private void OnPinPIRValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (sender.Read() == GpioPinValue.Low)
            {
                return;
            }

            switch (sender.PinNumber)
            {
                case (int)MotionDetectorID.Device_1:
                    passCounterManager.AddMove(Direction.In);
                    break;
                case (int)MotionDetectorID.Device_2:
                    passCounterManager.AddMove(Direction.Out);
                    break;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        private void OnCounterChanged(int i)
        {
            var index = i == -1 ? passCounterManager.Counter + 1 : passCounterManager.Counter;
            index = Clamp(index, 0, leds.Count);

            if (index < leds.Count)
            {
                var ledId = hackLedID[index];
                FlipLed(leds[(LedID)ledId]);
            }
        }

        private void FlipLed(GpioPin pin)
        {
            var readValue = pin.Read();
            GpioPinValue writeValue = readValue;

            switch (readValue)
            {
                case GpioPinValue.Low:
                    writeValue = GpioPinValue.High;
                    break;
                case GpioPinValue.High:
                    writeValue = GpioPinValue.Low;
                    break;
            }

            pin.Write(writeValue);
        }
    }
}
