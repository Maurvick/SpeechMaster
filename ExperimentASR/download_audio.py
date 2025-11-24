import yt_dlp

def download_audio(url, output_path):
    ydl_opts = {
        "format": "bestaudio/best",
        "outtmpl": "audio.%(ext)s",
        "postprocessors": [
            {
                "key": "FFmpegExtractAudio",
                "preferredcodec": "wav",
                "preferredquality": "192",
            }
        ],
        "quiet": True,
    }
    with yt_dlp.YoutubeDL(ydl_opts) as ydl:
        ydl.download([url])
    return "audio.wav"
