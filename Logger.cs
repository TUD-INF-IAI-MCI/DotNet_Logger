using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
using System.IO;
using System.Windows.Forms;

namespace tud.mci.tangram
{
    public class Logger : IDisposable
    {
        #region Member

        #region private

        volatile bool _run;
        Thread _outputQueueThread;
        readonly object threadLock = new object();
        Thread outputQueueThread
        {
            get { lock (threadLock) { return _outputQueueThread; } }
            set { lock (threadLock) { _outputQueueThread = value; } }
        }
        static readonly Queue _outputQueue = new Queue();
        static Queue OutputQueue = Queue.Synchronized(_outputQueue);

        const int maximumAttempts = 10;
        const int attemptWaitMS = 10;
        const int writerLogTimeout = 200;

        static readonly ReaderWriterLock rwl = new ReaderWriterLock();

        private static readonly Logger _instance = new Logger();

        #endregion

        #region public

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static Logger Instance { get { return _instance; } }
        private String _logPath;
        /// <summary>
        /// Gets or sets the directory path to the log file directory. 
        /// The file itself will be called 'log.log'.
        /// </summary>
        /// <value>
        /// The directory path.
        /// </value>
        public String LogPath
        {
            get { return _logPath; }
            set { _logPath = value; isLogFileReady(); }
        }
        /// <summary>
        /// Gets or sets the priority threshold for what is written in the log file.
        /// </summary>
        /// <value>
        /// The priority threshold.
        /// </value>
        public LogPriority Priority { get; set; }

        #endregion

        #endregion

        #region Constructor & Destructor

        Logger()
        {
            LogPath = GetCurrentDllDirectory() + "\\log.log";
            Priority = LogPriority.MIDDLE;
            enqueue("___________________________________________________");
        }

        ~Logger()
        {
            try
            {
                while (OutputQueue.Count > 0) { }
                //TODO: do not break this hard, try to empty the queue
                _run = false;
                this.outputQueueThread.Abort();
            }
            catch { }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            this.outputQueueThread.Abort();
        }

        #endregion

        #region Queuing

        /// <summary>
        /// Enqueues the specified obj to the output queue.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns></returns>
        private bool enqueue(Object obj)
        {
            try
            {
                createOutputQueueThread();
                OutputQueue.Enqueue(obj);
            }
            catch
            {
                return false;
            }
            return true;
        }

        #region Thread
        void createOutputQueueThread()
        {
            _run = true;
            if (outputQueueThread != null && !(outputQueueThread.ThreadState == ThreadState.Aborted || outputQueueThread.ThreadState == ThreadState.AbortRequested))
            {
                try
                {
                    if (outputQueueThread != null && outputQueueThread.IsAlive) return;
                    else if (outputQueueThread != null) outputQueueThread.Start();
                    else buildThread();
                }
                catch (ThreadStartException) { buildThread(); }
            }
            else { buildThread(); }
        }

        private void buildThread()
        {
            outputQueueThread = new Thread(new ThreadStart(checkOutputQueue));
            outputQueueThread.Name = "TangramLectorAudioQueueThread";
            outputQueueThread.IsBackground = true;
            outputQueueThread.Start();
        }

        void checkOutputQueue()
        {
            while (_run)
            {
                if (OutputQueue.Count > 0)
                {
                    try
                    {
                        handleQueueItem(OutputQueue.Dequeue());
                    }
                    catch { }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }

        /// <summary>
        /// Handles the queued items.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns></returns>
        private bool handleQueueItem(Object obj)
        {
            try
            {
                String msg;
                if (obj is LogMsg)
                {
                    if (((LogMsg)obj).Priority <= Priority)
                    {
                        msg = ((LogMsg)obj).ToString();
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    msg = obj.ToString();
                }
                writeToLogFile(msg);
                return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Writes to message to the log file.
        /// </summary>
        /// <param name="msg">The MSG.</param>
        private void writeToLogFile(string msg)
        {
            if (!String.IsNullOrWhiteSpace(msg))
            {
                rwl.AcquireWriterLock(writerLogTimeout);
                try
                {
                    int attempts = 0;
                    // Loop allow multiple attempts
                    while (true)
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write))
                            {
                                using (StreamWriter sw = new StreamWriter(fs))
                                {
#if DEBUG
    System.Diagnostics.Debug.WriteLine("\tLOG: " + msg);
#endif

                                    sw.WriteLine(msg);
                                }
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            // IOExcception is thrown if the file is in use by another process.

                            // Check the number of attempts to ensure no infinite loop
                            attempts++;
                            if (attempts > maximumAttempts)
                            {
                                // Too many attempts,cannot Open File, break and return null 
                                System.Diagnostics.Debug.WriteLine("[ERROR] Cannot write to log file!");
                                //System.Windows.Forms.MessageBox.Show("No Logging possible!!!", "logging error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                            }
                            else
                            {
                                // Sleep before making another attempt
                                isLogFileReady();
                                Thread.Sleep(attemptWaitMS);
                            }
                        }
                    }
                }
                finally
                {
                    rwl.ReleaseWriterLock();
                }
            }
        }

        #endregion

        #endregion

        #region Logging Functions

        /// <summary>
        /// Logs the specified MSG to a file.
        /// </summary>
        /// <param name="msg">Additional message to declare what that log means.</param>
        public void Log(String msg) { Log(LogPriority.MIDDLE, null, msg); }
        /// <summary>
        /// Logs the specified MSG to a file.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="msg">Additional message to declare what that log means.</param>
        public void Log(LogPriority priority, Object sender, String msg)
        {
            enqueue(new LogMsg(priority, sender, msg));
        }
        /// <summary>
        /// Logs the specified MSG to a file.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">An optional occurred Exception.</param>
        public void Log(Object sender, Exception e) { enqueue(new LogMsg(sender, e)); }
        /// <summary>
        /// Logs the specified MSG to a file.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="e">An optional occurred Exception.</param>
        public void Log(LogPriority priority, Object sender, Exception e)
        {
            enqueue(new LogMsg(priority, sender, e));
        }
        /// <summary>
        /// Logs the specified MSG to a file.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="msg">Additional message to declare what that log means.</param>
        /// <param name="e">An optional occurred Exception.</param>
        public void Log(LogPriority priority, Object sender, String msg, Exception e)
        {
            enqueue(new LogMsg(priority, sender, msg, e));
        }

        #endregion


        /// <summary>
        /// Gets the current file path of this dll.
        /// </summary>
        /// <returns>file path of this dll</returns>
        public static string GetCurrentDllPath()
        {
            string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return path;
        }

        /// <summary>
        /// Gets the current directory path of this dll.
        /// </summary>
        /// <returns>directory path of this dll</returns>
        public static string GetCurrentDllDirectory()
        {
            string path = GetCurrentDllPath();
            return Path.GetDirectoryName(path);
        }

        /// <summary>
        /// Determines whether [is log file ready].
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if [is log file ready]; otherwise, <c>false</c>.
        /// </returns>
        private bool isLogFileReady()
        {
            if (!String.IsNullOrWhiteSpace(LogPath))
            {
                if (!File.Exists(LogPath))
                {
                    try
                    {
                        int seperator = LogPath.LastIndexOf('\\');
                        string dirPath = LogPath.Substring(0, Math.Max(seperator, 0));
                        string filename = LogPath.Substring(seperator + 1);
                        if (!String.IsNullOrWhiteSpace(dirPath)) { Directory.CreateDirectory(dirPath); }
                        if (!String.IsNullOrWhiteSpace(filename)) { File.Create(LogPath); }
                    }
                    catch{}
                }

                if (File.Exists(LogPath)) return true;
            }
            return false;
        }

    }

    struct LogMsg
    {
        #region Members

        /// <summary>
        /// The priority of the log message. The logger filters the messages to write by this priority levels.
        /// </summary>
        public readonly LogPriority Priority;
        /// <summary>
        /// Additional message to declare what that log means.
        /// </summary>
        public readonly String Message;
        /// <summary>
        /// The log message Date and Time
        /// </summary>
        public readonly DateTime Date;
        /// <summary>
        /// An optional occurred Exception
        /// </summary>
        public readonly Exception Exception;
        /// <summary>
        /// The sender object
        /// </summary>
        public readonly Object Sender;
        
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="LogMsg"/> struct.
        /// </summary>
        /// <param name="sender">The sending object of this message.</param>
        /// <param name="exception">The exception to be logged.</param>
        public LogMsg(Object sender, Exception exception) : this(LogPriority.IMPORTANT, sender, String.Empty, exception) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="LogMsg"/> struct.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sending object of this message.</param>
        /// <param name="exception">The exception to be logged.</param>
        public LogMsg(LogPriority priority, Object sender, Exception exception) : this(priority, sender, String.Empty, exception) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="LogMsg"/> struct.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sending object of this message.</param>
        /// <param name="message">The message to be logged.</param>
        public LogMsg(LogPriority priority, Object sender, String message) : this(priority, sender, message, null) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="LogMsg" /> struct.
        /// </summary>
        /// <param name="priority">The priority of the log message. The logger filters the messages to write by this priority levels.</param>
        /// <param name="sender">The sending object of this message.</param>
        /// <param name="message">Additional message to declare what that log means.</param>
        /// <param name="exception">An optional occurred Exception.</param>
        public LogMsg(LogPriority priority, Object sender, String message, Exception exception)
        {
            this.Date = DateTime.Now;
            this.Priority = priority;
            this.Message = message;
            this.Exception = exception;
            this.Sender = sender;
        }

        public override string ToString()
        {
            return
                Date.ToString("dd.MM.yyyy HH:mm:ss.fff")
                + " \t[" + Priority.ToString() + "]"
                + " \t(" +  (Sender != null ? (Sender is String ? Sender : Sender.GetType().Name): "UNKNOWN") + ") \t" 
                + Message
                + (Exception != null ? " \t Exception: " + Exception.ToString() : "");
        }

    }
    
    /// <summary>
    /// This enum defines the log-priority.
    /// </summary>
    public enum LogPriority : byte
    {
        /// <summary>
        /// Very important. Log should never happen.
        /// </summary>
        ALWAYS = 0,
        /// <summary>
        /// Important. Log will not happen often, like light errors, that could occur.
        /// </summary>
        IMPORTANT = 2,
        /// <summary>
        /// Middle Priority. Log will happen regularly, such as process starts.
        /// </summary>
        MIDDLE = 4,
        /// <summary>
        /// Unimportant. Log will happen often, like keyboard inputs and some events-calls.
        /// </summary>
        OFTEN = 6,
        /// <summary>
        /// Only for debug reasons. Log will happen very often, like checking loops and fast system events.
        /// This priority should not be logged in Release-Version or it should have a very good reason.
        /// </summary>
        DEBUG = 8
    };

}
