#if later
using Svelto.Tasks;

namespace Test
{
    class ServiceTask : IServiceTask
    {
        public bool isDone { get; private set; }

        public ServiceTask()
        {
            isDone = false;
        }

        public void Execute()
        {
            _delayTimer = new System.Timers.Timer
            {
                Interval = 1000,
                Enabled  = true
            };
            _delayTimer.Elapsed += _delayTimer_Elapsed;
            _delayTimer.Start();
        }

        public void OnComplete(Action action)
        {
            _onComplete += action;
        }

        void _delayTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            isDone = true;
            if (_onComplete != null)
                _onComplete();

            _delayTimer.Stop();
            _delayTimer = null;
        }

        System.Timers.Timer _delayTimer;
        Action              _onComplete;
    }
}
#endif