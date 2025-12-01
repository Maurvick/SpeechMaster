using System;
using System.Collections.Generic;
using System.Text;

namespace ExperimentASR.Models
{
    // Event args for finished event
    public class TranscriptionFinishedEventArgs : EventArgs
    {
        public TranscriptionResult Result { get; }
        public TranscriptionFinishedEventArgs(TranscriptionResult result) => Result = result;
    }
}
