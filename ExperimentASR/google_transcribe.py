from deepspeech import Model

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