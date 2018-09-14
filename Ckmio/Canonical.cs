using System;
using System.Threading;

namespace Ckmio{
    public class Canonical
    {
        private ManualResetEvent exit = new ManualResetEvent(false);
        public void Start()
        {
            new Thread(StartMe).Start();
        }

        public void Stop()
        {
            exit.Set();
        }

        public void StartMe()
        {
            exit.WaitOne();
            Console.WriteLine("I am stopping!");;
        }
    }
}