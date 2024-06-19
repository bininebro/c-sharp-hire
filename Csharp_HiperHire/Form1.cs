using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Csharp_HiperHire
{
    public partial class Form1 : Form
    {
        private static Dictionary<string, string[]> fileLogs = new Dictionary<string, string[]>();
        private static ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();     
        private CancellationTokenSource cancellationTokenSource; 
        private static string? logDirectory;

        public Form1()
        {
            InitializeComponent();
            logDirectory = AppDomain.CurrentDomain.BaseDirectory;
         
            cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => MonitorFile(cancellationTokenSource.Token));
        }

        private void MonitorFile(CancellationToken token)
        {
            foreach (var watcher in watchers)
            {
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.EnableRaisingEvents = true;
            }

            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(15000); // Wait for 15 seconds before processing changes
                ProcessBatch();
            }
           
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                string[] currentSet = File.ReadAllLines(e.FullPath);
                string[] previousSet = fileLogs[e.FullPath];

                foreach (var line in currentSet)
                {
                    if (!previousSet.Contains(line))
                    {
                        logQueue.Enqueue(line);
                    }
                }

                foreach (var line in previousSet)
                {
                    if (!currentSet.Contains(line))
                    {
                        logQueue.Enqueue( line);
                    }
                }

                fileLogs[e.FullPath] = currentSet;
                this.Invoke(new Action(() => ReportChanges(e.Name)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log file: {ex.Message}");
            }
        }

        private void ReportChanges(string fileName )
        {
            StringBuilder changes = new StringBuilder();
            while (logQueue.TryDequeue(out string logEntry))
            {
                changes.AppendLine(logEntry);
            }

            if (changes.Length > 0)
            {
                if (fileName != null)
                {
                    string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    dataGridViewChanges.Rows.Add(currentTime, fileName, changes.ToString());
                }
            }
        }

        private void ProcessBatch()
        {
            StringBuilder batchData = new StringBuilder();
            while (logQueue.TryDequeue(out var logEntry))
            {
                batchData.AppendLine(logEntry);
            }

            if (batchData.Length > 0)
            {
                ShareBatchData(batchData.ToString());
            }
        }

        private void ShareBatchData(string batchData)
        {
            string batchFileName = Path.Combine(logDirectory, $"Batch_{DateTime.Now:yyyyMMddHHmmss}.txt");
            File.WriteAllText(batchFileName, batchData);
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            openFileDialog.Filter = "Text Files (*.txt)|*.txt";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var watcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(openFileDialog.FileName),
                    Filter = Path.GetFileName(openFileDialog.FileName),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                string[] initialContent = File.ReadAllLines(openFileDialog.FileName);
               
                if (fileLogs.ContainsKey(openFileDialog.FileName))
                {
                    fileLogs.Add(openFileDialog.FileName, initialContent);
                }
                else
                {
                    fileLogs[openFileDialog.FileName]= initialContent;
                }
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.EnableRaisingEvents = true;

                watchers.Add(watcher);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            timer.Stop();
        }
    }
}