using SpeechMaster.Models.Transcription;
using System.Collections.ObjectModel;

namespace SpeechMaster.Services
{
	// TODO: Review this class for potential improvements.
	public class TranscriptionQueueManager
    {
        private readonly TranscribeService _transcribeSerivce;

        // This collection binds directly to your UI
        public ObservableCollection<TranscriptionJob> Jobs { get; set; }
            = new ObservableCollection<TranscriptionJob>();

        private CancellationTokenSource _cts; // The trigger

        private bool _isProcessing = false;

        public TranscriptionQueueManager()
        {
            _transcribeSerivce = new TranscribeService();
        }

        public void AddFile(string path)
        {
            Jobs.Add(new TranscriptionJob
            {
                FileName = System.IO.Path.GetFileName(path),
                FilePath = path,
                Status = "Pending",
                Result = ""
            });
        }

        public async Task StartProcessing()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            // Create a new cancellation token source
            _cts = new CancellationTokenSource();

            try
            {
                while (Jobs.Any(j => j.Status == "Pending"))
                {
                    // 1. CHECK FOR CANCELLATION BEFORE STARTING A JOB
                    if (_cts.Token.IsCancellationRequested)
                    {
                        break; // Exit the loop immediately
                    }

                    // Get the next pending job
                    var currentJob = Jobs.First(j => j.Status == "Pending");

                    currentJob.Status = "Processing...";

                    var result = await Task.Run(() => StartTranscription(currentJob.FilePath));

                    // 2. CHECK AGAIN AFTER JOB FINISHES (to avoid updating UI if canceled)
                    if (_cts.Token.IsCancellationRequested)
                    {
                        currentJob.Status = "Pending"; // Reset status so it can be run again later
                        break;
                    }

                    currentJob.Result = result.Text;
                    currentJob.Status = result.Message;
                }

            }
            catch (OperationCanceledException)
            {
                // Handle if the task was killed mid-process
            }
            finally
            {
                _isProcessing = false;
                _cts.Dispose(); // Clean up
                _cts = null;
            }
        }
        public void CancelProcessing()
        {
            _cts?.Cancel(); // Signal the stop
        }

        private async Task<TranscriptionResult> StartTranscription(string path)
        {
            return await Task.Run(() => _transcribeSerivce.Transcribe(path));
        }
    }
}
