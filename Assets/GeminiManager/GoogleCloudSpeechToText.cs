using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSpeechToText.Scripts
{
    public class GoogleCloudSpeechToText : MonoBehaviour
    {
        // The API endpoint (including API key as a query parameter)
        private const string apiEndpoint = "https://speech.googleapis.com/v1/speech:recognize?&key=";

        // Sends a request to Google Speech-to-Text API
        // public static void SendSpeechToTextRequest(string audioUri, string apiKey, string accessToken, Action<string> onSuccess, Action<BadRequestData> onError)
        public static void SendSpeechToTextRequest(byte[] bytes, string apiKey, Action<string> onSuccess, Action<BadRequestData> onError)
        {
            string base64Content = Convert.ToBase64String(bytes);

            

            var requestData = new SpeechToTextRequest
            {
                config = new SpeechConfig
                {
                    encoding = "LINEAR16",
                    // sampleRateHertz = 16000,
                    languageCode = "en-US",
                    enableWordTimeOffsets = false
                },
                audio = new AudioData
                {
                    // uri = audioUri
                    content = base64Content,
                }
            };

            // Set headers for the request
            var headers = new Dictionary<string, string>
            {
                // { "Authorization", "Bearer " + accessToken },  // Use OAuth 2.0 Access Token here
                { "Content-Type", "application/json; charset=utf-8" }
            };

            // Format the endpoint with the provided API key
            string url =  apiEndpoint + apiKey;

            // Serialize request data to JSON
            string requestJson = JsonUtility.ToJson(requestData);

            // Call the Post method to send the request
            Post(url, requestJson, onSuccess, onError, headers);
        }

        private static async void Post(string url, string bodyJsonString, Action<string> onSuccess, Action<BadRequestData> onError, Dictionary<string, string> headers)
        {
            var request = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Add headers to the request
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }

            var operation = request.SendWebRequest();

            // Wait for the request to complete
            while (!operation.isDone)
                await Task.Yield();

            // Check for errors
            if (HasError(request, out var badRequest))
            {
                onError?.Invoke(badRequest);
            }
            else
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }

            request.Dispose();
        }

        private static bool HasError(UnityWebRequest request, out BadRequestData badRequestData)
        {
            if (request.responseCode == 200 || request.responseCode == 201)
            {
                badRequestData = null;
                return false;
            }

            badRequestData = JsonUtility.FromJson<BadRequestData>(request.downloadHandler.text);
            try
            {
                badRequestData = JsonUtility.FromJson<BadRequestData>(request.downloadHandler.text);
                return true;
            }
            catch (Exception)
            {
                badRequestData = new BadRequestData
                {
                    error = new Error
                    {
                        code = (int)request.responseCode,
                        message = request.error
                    }
                };
                return true;
            }
        }
    }

    // The request data format for Google Speech-to-Text API
    [System.Serializable]
    public class SpeechToTextRequest
    {
        public SpeechConfig config;
        public AudioData audio;
    }

    [System.Serializable]
    public class SpeechConfig
    {
        public string encoding;
        public int sampleRateHertz;
        public string languageCode;
        public bool enableWordTimeOffsets;
    }

    [System.Serializable]
    public class AudioData
    {
        // public string uri;
        public string  content;
    }

    // Response format for Google Speech-to-Text API
    [System.Serializable]
    public class SpeechToTextResponse
    {
        public Result[] results;
    }

    [System.Serializable]
    public class Result
    {
        public Alternative[] alternatives;
    }

    [System.Serializable]
    public class Alternative
    {
        public string transcript;
        public float confidence;
    }

    // Error response format
    [System.Serializable]
    public class BadRequestData
    {
        public Error error;
    }

    [System.Serializable]
    public class Error
    {
        public int code;
        public string message;
    }
}
