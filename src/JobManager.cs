using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class JobManager
    {
        private readonly object gate = new object();
        private readonly SynchronizationContext uiContext;
        private readonly UndoStore undoStore;
        private readonly List<JobEntry> jobs = new List<JobEntry>();

        public JobManager(UndoStore history)
        {
            undoStore = history;
            uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        public event EventHandler Changed;
        public event EventHandler<JobCompletedEventArgs> Completed;

        public IList<JobEntry> Jobs
        {
            get { lock (gate) return jobs.ToList().AsReadOnly(); }
        }

        public int RunningCount
        {
            get { lock (gate) return jobs.Count(item => item.Status == JobStatus.Running || item.Status == JobStatus.Waiting); }
        }

        public void Enqueue(string title, Func<ActionResult> work)
        {
            var job = new JobEntry { Title = title, Status = JobStatus.Waiting, CreatedAt = DateTime.Now };
            lock (gate)
            {
                jobs.Insert(0, job);
                if (jobs.Count > 30) jobs.RemoveRange(30, jobs.Count - 30);
            }
            RaiseChanged();
            Task.Run(() => Execute(job, work));
        }

        private void Execute(JobEntry job, Func<ActionResult> work)
        {
            job.Status = JobStatus.Running;
            RaiseChanged();
            ActionResult result = null;
            Exception error = null;
            try
            {
                result = work() ?? new ActionResult { Message = "处理完成" };
                job.Status = JobStatus.Completed;
                job.Message = result.Message;
                job.Outputs = result.Outputs == null ? new List<string>() : result.Outputs.ToList();
                if (result.Outputs != null && result.Outputs.Count > 0) undoStore.Record(job.Title, result.Outputs);
            }
            catch (Exception exception)
            {
                error = exception;
                job.Status = JobStatus.Failed;
                job.Message = exception.Message;
            }
            job.CompletedAt = DateTime.Now;
            RaiseChanged();
            uiContext.Post(delegate
            {
                var handler = Completed;
                if (handler != null) handler(this, new JobCompletedEventArgs(job, result, error));
            }, null);
        }

        private void RaiseChanged()
        {
            uiContext.Post(delegate
            {
                var handler = Changed;
                if (handler != null) handler(this, EventArgs.Empty);
            }, null);
        }
    }

    internal enum JobStatus { Waiting, Running, Completed, Failed }

    internal sealed class JobEntry
    {
        public JobEntry() { Outputs = new List<string>(); }
        public string Title { get; set; }
        public string Message { get; set; }
        public JobStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public List<string> Outputs { get; set; }

        public override string ToString()
        {
            var marker = Status == JobStatus.Completed ? "✓" : Status == JobStatus.Failed ? "!" : "●";
            return marker + "  " + Title + (string.IsNullOrWhiteSpace(Message) ? "" : "  ·  " + Message);
        }
    }

    internal sealed class JobCompletedEventArgs : EventArgs
    {
        public JobCompletedEventArgs(JobEntry job, ActionResult result, Exception error)
        {
            Job = job;
            Result = result;
            Error = error;
        }
        public JobEntry Job { get; private set; }
        public ActionResult Result { get; private set; }
        public Exception Error { get; private set; }
    }
}
