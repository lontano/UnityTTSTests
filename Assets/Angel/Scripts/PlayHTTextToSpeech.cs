using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using UnityEditor;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PlayhtTTS;
using TreeEditor;
using UnityEngine.Rendering.UI;
using UnityEngine.UI;
using System.IO;

public class PlayHTTextToSpeech : MonoBehaviour
{
    [Header("PlayHT")]
    private const string ApiKeyPrefKey = "APIKey";
    private const string UserIDPrefKey = "UserID";
    public string apiKey = "";
    public string userID = "";
    public string voiceID = "en-GB_KateV3Voice";

    [Header("Options")]
    public PlayhtTTS.PlayhtInterface playhtInterface;
    public AudioClip audioClip;
    public event EventHandler<ResponsePoolEventArgs> ResponsePoolReceived;
    public bool isPlaying = false;
    private string inputText = "This is some sample text";
    public bool CopyToLocalStorage = true;
    public string remoteFile = "";
    public string localFile = "";

    private void OnEnable()
    {
        playhtInterface = new PlayhtInterface();
        playhtInterface.ResponseReceived += OnResponseReceived;
        playhtInterface.RequestSent += OnRequestSent;
        playhtInterface.ResponsePoolReceived += OnResponsePoolReceived;
    }

    private void OnDisable()
    {
        playhtInterface.ResponseReceived -= OnResponseReceived;
        playhtInterface.RequestSent -= OnRequestSent;
        playhtInterface.ResponsePoolReceived -= OnResponsePoolReceived;
    }

    private void Start()
    {
        LoadSettings();
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(ApiKeyPrefKey))
        {
            apiKey = PlayerPrefs.GetString(ApiKeyPrefKey);
        }

        if (PlayerPrefs.HasKey(UserIDPrefKey))
        {
            userID = PlayerPrefs.GetString(UserIDPrefKey);
        }
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetString(ApiKeyPrefKey, apiKey);
        PlayerPrefs.SetString(UserIDPrefKey, userID);
        PlayerPrefs.Save();
    }

    private string _debugLog = "";
    private void DebugLog(string msg)
    {
        _debugLog = DateTime.Now.ToString() + ": " + msg + "\n" + _debugLog;
        Debug.Log(msg);
    }
    private void DebugLogError(string msg)
    {
        _debugLog = DateTime.Now.ToString() + ": " + msg + "\n" + _debugLog;
        Debug.Log(msg);
    }

    public IEnumerator ConvertTextToSpeech(string text)
    {
        playhtInterface.apiKey = apiKey;
        playhtInterface.userID = userID;
        playhtInterface.voiceID = voiceID;

        playhtInterface?.SendRequest(text);
        yield return new WaitForEndOfFrame();
    }

    protected void OnResponseReceived(object obj, ResponseEventArgs e)
    {
        DebugLog($"Response received{e.response.Replace("\n", Environment.NewLine)}");
    }

    protected void OnRequestSent(object obj, ResponseEventArgs e)
    {
        DebugLog($"Request sent {e.request}");
    }
    protected void OnResponsePoolReceived(object obj, ResponsePoolEventArgs e)
    {
        DebugLog($"OnResponsePoolReceived {e.response.message}");
        ResponsePoolReceived?.Invoke(this, e);
        if (e.isFinished)
        {
            DebugLog($"TTS Finished {e.response.audioUrl}");

            if (CopyToLocalStorage)
            {
                string localFileName = GetLocalFileName(e.response.audioUrl);
                localFile = Path.Combine(Application.persistentDataPath, localFileName);
                remoteFile = e.response.audioUrl;
                DebugLog($"Downloading file from {remoteFile} to {localFile}");
                StartCoroutine(DownloadFile(remoteFile, localFile));
            }
        }
    }
    private string GetLocalFileName(string url)
    {
        int questionMarkIndex = url.IndexOf('?');
        if (questionMarkIndex >= 0)
        {
            url = url.Substring(0, questionMarkIndex);
        }

        // Remove directory path separators ("/" or "\") and keep only the file name
        string[] pathParts = url.Split('/', '\\');
        string fileName = pathParts[pathParts.Length - 1];

        return fileName;
    }


    private IEnumerator DownloadFile(string url, string filePath)
    {
        Debug.Log ($"Downloading file {url} to {filePath}");
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                byte[] audioData = www.downloadHandler.data;
                File.WriteAllBytes(filePath, audioData);
                DebugLog($"File downloaded: {filePath}");
                //PlayAudioFile(filePath);
            }
            else
            {
                DebugLogError($"Failed to download file from {url}: {www.error}");
            }
        }
    }

    private void PlayAudioFile(string filePath)
    {
        StartCoroutine(LoadAudioClip(filePath));
    }
    private IEnumerator LoadAudioClip(string filePath)
    {
        DebugLog($"Loading Audio Clip from {filePath}");

        // Check if the filePath starts with "http://" or "https://" to determine if it's a remote URL
        if (filePath.StartsWith("http://") || filePath.StartsWith("https://"))
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    AudioSource audioSource = GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        DebugLog($"Creating audioSource");
                        audioSource = gameObject.AddComponent<AudioSource>();
                    }
                    audioSource.clip = audioClip;
                    audioSource.Play();
                    isPlaying = true;
                    DebugLog($"Audio clip loaded and playing: {filePath}");
                }
                else
                {
                    DebugLogError($"Failed to load audio clip from {filePath}: {www.error}");
                }
            }
        }
        else
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    AudioSource audioSource = GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        DebugLog($"Creating audioSource");
                        audioSource = gameObject.AddComponent<AudioSource>();
                    }
                    audioSource.clip = audioClip;
                    audioSource.Play();
                    isPlaying = true;
                    DebugLog($"Audio clip loaded and playing: {filePath}");
                }
                else
                {
                    DebugLogError($"Failed to load audio clip from {filePath}: {www.error}");
                }
            }
        }
    }


    private void OnGUI()
    {
        GUILayout.Label("Text to Convert:");
        inputText = GUILayout.TextArea(inputText);

        GUILayout.Label("API Key:");
        apiKey = GUILayout.TextField(apiKey);

        GUILayout.Label("User ID:");
        userID = GUILayout.TextField(userID);

        GUILayout.Label("Voice ID:");
        voiceID = GUILayout.TextField(voiceID);

        GUILayout.Label("Copy to Local Storage:");
        CopyToLocalStorage = GUILayout.Toggle(CopyToLocalStorage, "Copy to Local");

        if (GUILayout.Button("Convert"))
        {
            TTS(inputText);
        }

        if (localFile != "")
        {
            if (GUILayout.Button("Play Local"))
            {
                PlayAudioFile(localFile);
            }
        }

        if (remoteFile != "")
        {
            if (GUILayout.Button("Play Remote"))
            {
                PlayAudioFile(remoteFile);
            }
        }

        GUILayout.Label(remoteFile);
        GUILayout.Label(localFile);
        GUILayout.TextArea(_debugLog);
    }

    public void TTS(string text)
    {
        DebugLog($"Starting TTS {text}");
        StartCoroutine(ConvertTextToSpeech(text));
    }
}

namespace PlayhtTTS
{
    public class ResponseEventArgs : EventArgs
    {
        public string prompt;
        public string request;
        public string response;
        public bool isCompleteResponse;
    }

    public class ResponsePoolEventArgs : EventArgs
    {
        public string Status;
        public string progress;
        public string output;
        public ResponsePool response;
        public string localFile;
        public string message;
        public bool isFinished = false;
    }

    public class PlayhtInterface
    {
        public string apiKey = "";
        public string userID = "";
        public string voiceID = "en-GB_KateV3Voice";
        public string localFolder = @"C:\Temp";

        
        
        public event EventHandler<ResponseEventArgs> ResponseReceived;
        public event EventHandler<ResponseEventArgs> RequestSent;
        public event EventHandler<ResponsePoolEventArgs> ResponsePoolReceived;

        public List<TextRequest> TextRequests = new List<TextRequest>();

        public string BuildRequest(string question)
        {
            string[] content = new string[] { question };

            string voice = voiceID;
            string narrationStyle = "";
            string globalSpeed = "100%";
            string title = "Racino test " + voice + " " + narrationStyle + " " + globalSpeed  + " " + question;
            string preset = "high-quality";
            string project = "Racino";

            var requestBody = new
            {
                voice,
                content,
                title,
                narrationStyle,
                globalSpeed,
                preset,
                project
            };
            var json = JsonConvert.SerializeObject(requestBody);
            return json;
        }


        public async Task<TextRequest> SendRequest(string textToPlay)
        {
            TextRequest myRequest = new TextRequest();

            myRequest.Text = textToPlay;
            myRequest.LanguageID = voiceID;

            string requestData = BuildRequest(textToPlay);
            string url = "https://play.ht/api/v1/convert";

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", apiKey);
            client.DefaultRequestHeaders.Add("X-User-ID", userID);

            var requestContent = new StringContent(requestData, Encoding.UTF8, "application/json");

            var httpResponseMessage = await client.PostAsync(url, requestContent);

            Debug.Log($"Posting request to {url} data {requestData}");

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                string responseString = await httpResponseMessage.Content.ReadAsStringAsync();
                Response response = JsonConvert.DeserializeObject<Response>(responseString);
                //OnResponseReceived(new ResponseEventArgs() { request = requestData, response = "", isCompleteResponse = true });

                ResponsePool processResult = await PollForAudioFile(response.transcriptionId);

                if (processResult != null)
                {
                    myRequest.TransactionID = processResult.transcriptionId;
                    myRequest.URL = processResult.audioUrl;
                    myRequest.Duration = processResult.audioDuration;
                }

                this.TextRequests.Add(myRequest);

                return myRequest;
            }
            else
            {
                OnResponseReceived(new ResponseEventArgs() { request = requestData, response = $"Error: {httpResponseMessage.StatusCode} - {httpResponseMessage.ReasonPhrase}", isCompleteResponse = true });
                throw new Exception($"Failed to convert text to speech. Status code: {httpResponseMessage.StatusCode}");
            }
        }

        public string localFile = "";

        public async Task<ResponsePool> PollForAudioFile(string transcriptionId)
        {
            ResponsePool response = new ResponsePool();

            bool isComplete = false;
            while (!isComplete)
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", this.apiKey);
                    httpClient.DefaultRequestHeaders.Add("X-User-ID", this.userID);

                    var url = $"https://play.ht/api/v1/articleStatus?transcriptionId={transcriptionId}";
                    var httpResponseMessage = await httpClient.GetAsync(url);

                    Debug.Log($"Polling for completion on {url}");

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        string jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                        try
                        {
                            response = JsonConvert.DeserializeObject<ResponsePool>(jsonResponse);
                        }
                        catch (Exception ex)
                        {
                            response = null;   
                        }
                        
                        if (response.status == "SUCCESS" || response.converted == true)
                        {
                            isComplete = true;
                        }

                        response.transcriptionId = transcriptionId;
                        ResponsePoolReceived?.Invoke(this,new ResponsePoolEventArgs(){isFinished = isComplete, response = response});
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(0.25)); // Wait before polling again

            }
            return response;
        }



        protected virtual void OnResponseReceived(ResponseEventArgs e)
        {
            ResponseReceived?.Invoke(this, e);
        }
        protected virtual void OnRequestSent(ResponseEventArgs e)
        {
            RequestSent?.Invoke(this, e);
        }
        protected virtual void OnPoolReceived(ResponsePoolEventArgs e)
        {
            ResponsePoolReceived?.Invoke(this, e);
        }


    }

    public class Request
    {
        public string voice { get; set; } = "en-US-MichelleNeural";
        public string[] Content { get; set; }
    }

    public class Response
    {
        public string status { get; set; }
        public string transcriptionId { get; set; }
        public int contentLength { get; set; }
        public int wordCount { get; set; }
    }

    public class ResponsePool
    {
        public string status { get; set; }
        public string transcriptionId { get; set; }
        public ResponsePoolMetadata metadata { get; set; }

        public string voice { get; set; }
        public string narrationStyle { get; set; }
        public string globalSpeed { get; set; }
        public bool converted { get; set; }
        public double audioDuration { get; set; }
        public string audioUrl { get; set; }
        public string message { get; set; }
        
    }

    public class ResponsePoolMetadata
    {
        public double progress;
        public string output;
    }

    public class TextRequest
    {
        public string uid = Guid.NewGuid().ToString();
        public string Text = "";
        public string LanguageID = "";
        public string TransactionID = "";
        public string URL = "";
        public double Duration = 0f;
        public int wordCount = 0;
    }
}