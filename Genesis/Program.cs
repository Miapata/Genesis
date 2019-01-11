using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Speech.V1;
using System.Speech.Synthesis;
using System.Device.Location;

namespace Genesis
{
    class Program
    {

        public static string speechResult;
        static void Main(string[] args)
        {
            var client = new OpenWeatherAPI.OpenWeatherAPI("6ac232d2dac00bdaa191f6d3db9cbeec");

            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.SelectVoiceByHints(VoiceGender.Female);
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "C:\\Users\\Mi'Angel\\Downloads\\Genesis-1ac8c357b5fb.json");

            synthesizer.Speak("Hello. I'm Genesis.");
            StreamingMicRecognizeAsync(2).Wait();
            Console.WriteLine(speechResult);

            if (speechResult.Contains("weather"))
            {
                // Get location

                var city = "Phoenix, Arizona";
                //Get API result
                //To get tomorrow or yesterdays weather or a forecast, we can use a switch statement to return a result
                //as a subContains if speech aslo contains today, tomorrow 




                synthesizer.Speak($"Fetching weather data for '{city}'");
                var results = client.Query(city);

                synthesizer.Speak($"The temperature in {city} is {results.Main.Temperature.FahrenheitCurrent} degrees. There is {results.Wind.SpeedFeetPerSecond.ToString("0")} feet per second wind in the {results.Wind.Direction} direction.");
            }
        }
        static async Task<object> StreamingMicRecognizeAsync(int seconds)
        {
            if (NAudio.Wave.WaveIn.DeviceCount < 1)
            {
                Console.WriteLine("No microphone!");
                return -1;
            }
            var speech = SpeechClient.Create();
            var streamingCall = speech.StreamingRecognize();
            // Write the initial request with the config.
            await streamingCall.WriteAsync(
                new StreamingRecognizeRequest()
                {
                    StreamingConfig = new StreamingRecognitionConfig()
                    {
                        Config = new RecognitionConfig()
                        {
                            Encoding =
                            RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = "en",
                        },
                        InterimResults = true,
                    }
                });
            // Print responses as they arrive.
            Task printResponses = Task.Run(async () =>
            {

                Google.Protobuf.Collections.RepeatedField<SpeechRecognitionAlternative> list = new Google.Protobuf.Collections.RepeatedField<SpeechRecognitionAlternative>();
                while (await streamingCall.ResponseStream.MoveNext(
                    default(CancellationToken)))
                {
                    foreach (var result in streamingCall.ResponseStream
                        .Current.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            list.Add(alternative);

                        }
                    }

                }
                speechResult = list[list.Count - 1].Transcript;
            });
            // Read from the microphone and stream to API.
            object writeLock = new object();
            bool writeMore = true;
            var waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable +=
                (object sender, NAudio.Wave.WaveInEventArgs args) =>
                {
                    lock (writeLock)
                    {
                        if (!writeMore) return;
                        streamingCall.WriteAsync(
                            new StreamingRecognizeRequest()
                            {
                                AudioContent = Google.Protobuf.ByteString
                                    .CopyFrom(args.Buffer, 0, args.BytesRecorded)
                            }).Wait();
                    }
                };
            waveIn.StartRecording();
            Console.WriteLine("Speak now.");
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            // Stop recording and shut down.
            waveIn.StopRecording();
            lock (writeLock) writeMore = false;
            await streamingCall.WriteCompleteAsync();
            await printResponses;
            return 0;
        }


    }
}
