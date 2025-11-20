import whisper
from google.cloud import speech
import sys
import json
import jiwer

from whisper import transcribe

def send_error(msg):
    print(json.dumps({"status": "error", "message": msg}))
    sys.exit(1)

if len(sys.argv) < 2:
    send_error("Usage: python YoutubeTranscriptCLI.py <URL> <lang>")


url = sys.argv[1]
lang = sys.argv[2]
asr_model= sys.argv[3] if len(sys.argv) > 3 else "whisper"
whisper_model = {
    0: "tiny",
    1: "base",
    2: "small",
    3: "medium",
    4: "large"
}

def transcribe_google(audio_file_path):
    client = speech.SpeechClient()

    with open(audio_file_path, "rb") as f:
        audio = speech.RecognitionAudio(content=f.read())

    config = speech.RecognitionConfig(
        language_code="uk-UA",
        enable_automatic_punctuation=True
    )

    response = client.recognize(config=config, audio=audio)

    result_text = ""
    for result in response.results:
        result_text += result.alternatives[0].transcript + " "

    return result_text.strip()

def transcribe_audio(file_path, lang="en"):
    print(json.dumps({
    "status": "info",
    "message": f'Initiating {file_path} transcription...'
    }))
    model = whisper.load_model(whisper_model[1])
    result = model.transcribe(
        file_path,
        language=lang,          # None = auto-detect
        task="transcribe",      # or "translate" to English
        temperature=0.0,
        best_of=5,
        beam_size=5,
        fp16=True  # Set True if you have GPU
    )
    return result["text"]

def evaluate_wer_cer(reference_text, recognized_text):
    wer = jiwer.wer(reference_text, recognized_text)
    cer = jiwer.cer(reference_text, recognized_text)
    return wer, cer

transcript = transcribe_audio(url, lang)

print(json.dumps({
    "status": "ok",
    "transcript": transcript
}))
