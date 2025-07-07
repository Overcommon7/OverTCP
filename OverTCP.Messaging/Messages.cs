using System;
using System.Collections.Generic;

namespace OverTCP.Messaging
{
    public enum Messages : int
    {
        Placeholder,

        FileData = 100_000,
        DirectoryData,
        FileCount,
        ReadyForFiles, 
        FileDataError,
        PauseFileChunks,
        ResumeFileChunks,
        DirectoryTransferComplete
    }
}
