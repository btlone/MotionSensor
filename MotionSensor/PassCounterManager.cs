using System.Collections.Generic;
using MotionSensor.Enums;

namespace MotionSensor
{
    public class PassCounterManager
    {
        private readonly List<Direction> moves;
        public int Counter { get; private set; }

        public delegate void TypedEventHandler(int i);
        public event TypedEventHandler CounterChanged;

        public PassCounterManager()
        {
            moves = new List<Direction>();
            Counter = -1;
        }

        public void AddMove(Direction dir)
        {
            moves.Add(dir);

            if (moves.Count >= 2)
            {
                if (moves[0] == moves[1])
                {
                    moves.Clear();
                    return;
                }

                int tmpValue = moves[0] == Direction.In ? 1 : -1;
                Counter += tmpValue;
                moves.Clear();

                if (Counter < -1)
                {
                    Counter = -1;
                    return;
                }

                CounterChanged?.Invoke(tmpValue);
            }
        }
    }
}
