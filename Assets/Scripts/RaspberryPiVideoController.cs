using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RISCOM.RaspberryPi
{
    /// <summary>
    /// Video Player Controller designed for Linux / Raspberry Pi builds.
    /// Supports switching between Video A, B, and C via keyboard input (Keys A, B, C)
    /// and UI buttons, with robust audio routing and prepared playback.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class RaspberryPiVideoController : MonoBehaviour
    {
        public enum VideoSelection
        {
            None,
            VideoA,
            VideoB,
            VideoC
        }

        [Header("Video Clips (Inspector Assignment)")]
        [Tooltip("Video A Clip")]
        public VideoClip videoClipA;
        [Tooltip("Video B Clip")]
        public VideoClip videoClipB;
        [Tooltip("Video C Clip")]
        public VideoClip videoClipC;

        [Header("Fallback Video File Paths (For Linux / Disk Playback)")]
        [Tooltip("Path relative to project or absolute path for Video A")]
        public string videoPathA = "Assets/Videos/2026-03-04 16-49-10.mkv";
        [Tooltip("Path relative to project or absolute path for Video B")]
        public string videoPathB = "Assets/Videos/Micro Labs Aziderm Video SEP2025 17092025 V2 F1 LR.mp4";
        [Tooltip("Path relative to project or absolute path for Video C")]
        public string videoPathC = "Assets/Videos/Micro Labs Bangkok Event Out V5 (3).mp4";

        [Header("Video Names / Titles")]
        public string titleA = "Video A - System Demo";
        public string titleB = "Video B - Micro Labs Aziderm";
        public string titleC = "Video C - Micro Labs Bangkok Event";

        [Header("UI Components")]
        [Tooltip("RawImage target for rendering video texture")]
        public RawImage displayRawImage;
        [Tooltip("Aspect Ratio Fitter for correct video aspect ratio")]
        public AspectRatioFitter aspectRatioFitter;
        [Tooltip("Text component to show current video title & status")]
        public TextMeshProUGUI statusText;
        [Tooltip("Text component to show keyboard instructions")]
        public TextMeshProUGUI instructionsText;
        [Tooltip("Optional panel to show toast / status overlay")]
        public GameObject overlayPanel;

        [Header("Audio Settings")]
        [Tooltip("AudioSource component for clean Linux audio routing")]
        public AudioSource audioSource;

        [Header("Playback Options")]
        public bool loopVideo = true;
        public bool playVideoAOnStart = true;
        public VideoSelection currentSelection = VideoSelection.None;

        // Internal fields
        private VideoPlayer videoPlayer;
        private RenderTexture renderTexture;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Auto-assign RawImage if unassigned
            if (displayRawImage == null)
            {
                displayRawImage = GetComponentInChildren<RawImage>();
                if (displayRawImage == null)
                {
#if UNITY_6000_0_OR_NEWER
                    displayRawImage = FindFirstObjectByType<RawImage>();
#else
                    displayRawImage = FindObjectOfType<RawImage>();
#endif
                }
            }

            // Auto-assign or attach AspectRatioFitter for correct video scaling
            if (displayRawImage != null)
            {
                if (aspectRatioFitter == null)
                {
                    aspectRatioFitter = displayRawImage.GetComponent<AspectRatioFitter>();
                    if (aspectRatioFitter == null)
                    {
                        aspectRatioFitter = displayRawImage.gameObject.AddComponent<AspectRatioFitter>();
                    }
                }

                if (aspectRatioFitter != null)
                {
                    aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                }
            }

            // Auto-assign UI text components if unassigned
            if (statusText == null || instructionsText == null)
            {
#if UNITY_6000_0_OR_NEWER
                var textComponents = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
#else
                var textComponents = FindObjectsOfType<TextMeshProUGUI>();
#endif
                foreach (var txt in textComponents)
                {
                    if (statusText == null && txt.gameObject.name.Contains("Status"))
                    {
                        statusText = txt;
                    }
                    else if (instructionsText == null && txt.gameObject.name.Contains("Instructions"))
                    {
                        instructionsText = txt;
                    }
                }
            }
        }

        private void Start()
        {
            SetupVideoPlayer();
            SetupRenderTexture();
            UpdateInstructionsUI();

            if (playVideoAOnStart)
            {
                PlayVideoA();
            }
            else
            {
                UpdateStatusUI("Press Key A, B, or C to start playback", "");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (displayRawImage == null)
            {
                displayRawImage = GetComponentInChildren<RawImage>();
                if (displayRawImage == null)
                {
                    displayRawImage = FindFirstObjectByType<RawImage>();
                }
            }

            if (displayRawImage != null && aspectRatioFitter == null)
            {
                aspectRatioFitter = displayRawImage.GetComponent<AspectRatioFitter>();
            }

            if (statusText == null || instructionsText == null)
            {
                var textComponents = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
                foreach (var txt in textComponents)
                {
                    if (statusText == null && txt.gameObject.name.Contains("Status"))
                    {
                        statusText = txt;
                    }
                    else if (instructionsText == null && txt.gameObject.name.Contains("Instructions"))
                    {
                        instructionsText = txt;
                    }
                }
            }
        }
#endif

        private void Update()
        {
            HandleInput();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.errorReceived -= OnVideoError;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        /// <summary>
        /// Configures VideoPlayer properties for smooth Linux / Raspberry Pi playback.
        /// </summary>
        private void SetupVideoPlayer()
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loopVideo;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            
            // Configure AudioSource output for Linux ALSA / PulseAudio stability
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);

            // Subscribe to VideoPlayer events
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
        }

        /// <summary>
        /// Creates a RenderTexture formatted for display on the RawImage UI.
        /// </summary>
        private void SetupRenderTexture()
        {
            int width = Screen.width > 0 ? Screen.width : 1920;
            int height = Screen.height > 0 ? Screen.height : 1080;

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "RaspberryPiVideoRenderTexture",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            renderTexture.Create();

            videoPlayer.targetTexture = renderTexture;

            if (displayRawImage != null)
            {
                displayRawImage.texture = renderTexture;
                displayRawImage.color = Color.white;
            }
        }

        /// <summary>
        /// Input polling supporting both New Input System and Legacy Input.
        /// </summary>
        private void HandleInput()
        {
            bool keyAPressed = false;
            bool keyBPressed = false;
            bool keyCPressed = false;
            bool spacePressed = false;
            bool stopPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                keyAPressed = Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.digit1Key.wasPressedThisFrame;
                keyBPressed = Keyboard.current.bKey.wasPressedThisFrame || Keyboard.current.digit2Key.wasPressedThisFrame;
                keyCPressed = Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.digit3Key.wasPressedThisFrame;
                spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                stopPressed = Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            // Fallback check for legacy input if enabled in Player Settings
            if (!keyAPressed && !keyBPressed && !keyCPressed && !spacePressed && !stopPressed)
            {
                keyAPressed = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha1);
                keyBPressed = Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Alpha2);
                keyCPressed = Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Alpha3);
                spacePressed = Input.GetKeyDown(KeyCode.Space);
                stopPressed = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Escape);
            }
#endif

            if (keyAPressed)
            {
                PlayVideoA();
            }
            else if (keyBPressed)
            {
                PlayVideoB();
            }
            else if (keyCPressed)
            {
                PlayVideoC();
            }
            else if (spacePressed)
            {
                TogglePause();
            }
            else if (stopPressed)
            {
                StopVideo();
            }
        }

        #region Public Playback Controls (Called via Keys or UI Buttons)

        /// <summary>
        /// Plays Video A (Key A)
        /// </summary>
        public void PlayVideoA()
        {
            currentSelection = VideoSelection.VideoA;
            LoadAndPlayVideo(videoClipA, videoPathA, titleA);
        }

        /// <summary>
        /// Plays Video B (Key B)
        /// </summary>
        public void PlayVideoB()
        {
            currentSelection = VideoSelection.VideoB;
            LoadAndPlayVideo(videoClipB, videoPathB, titleB);
        }

        /// <summary>
        /// Plays Video C (Key C)
        /// </summary>
        public void PlayVideoC()
        {
            currentSelection = VideoSelection.VideoC;
            LoadAndPlayVideo(videoClipC, videoPathC, titleC);
        }

        /// <summary>
        /// Toggles play/pause state of current video (Space key)
        /// </summary>
        public void TogglePause()
        {
            if (videoPlayer == null) return;

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                if (audioSource != null) audioSource.Pause();
                UpdateStatusUI("Paused", GetSelectionTitle(currentSelection));
            }
            else if (videoPlayer.isPrepared)
            {
                videoPlayer.Play();
                if (audioSource != null) audioSource.UnPause();
                UpdateStatusUI("Playing", GetSelectionTitle(currentSelection));
            }
        }

        /// <summary>
        /// Stops video playback (S key or Escape key)
        /// </summary>
        public void StopVideo()
        {
            if (videoPlayer == null) return;

            videoPlayer.Stop();
            if (audioSource != null) audioSource.Stop();
            currentSelection = VideoSelection.None;
            UpdateStatusUI("Stopped", "Press A, B, or C to play video");
        }

        #endregion

        /// <summary>
        /// Loads a video clip asset or file URL and starts preparation.
        /// </summary>
        private void LoadAndPlayVideo(VideoClip clip, string relativePath, string title)
        {
            videoPlayer.Stop();
            if (audioSource != null) audioSource.Stop();

            UpdateStatusUI("Loading...", title);

            bool sourceSet = false;

            // 1. Try VideoClip reference if assigned
            if (clip != null)
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = clip;
                sourceSet = true;
                Debug.Log($"[RaspberryPiVideoPlayer] Playing VideoClip: {clip.name}");
            }
            // 2. Try file path if VideoClip is not set
            else if (!string.IsNullOrEmpty(relativePath))
            {
                string resolvedPath = ResolveFilePath(relativePath);
                if (File.Exists(resolvedPath) || resolvedPath.StartsWith("http"))
                {
                    videoPlayer.source = VideoSource.Url;
                    videoPlayer.url = resolvedPath;
                    sourceSet = true;
                    Debug.Log($"[RaspberryPiVideoPlayer] Playing Video URL/File: {resolvedPath}");
                }
                else
                {
                    Debug.LogWarning($"[RaspberryPiVideoPlayer] File not found at path: {resolvedPath}");
                }
            }

            if (sourceSet)
            {
                // Prepare asynchronously for smooth playback on Raspberry Pi
                videoPlayer.Prepare();
            }
            else
            {
                UpdateStatusUI("Error", $"Could not find clip or path for: {title}");
            }
        }

        /// <summary>
        /// Called when VideoPlayer finishes preparing the video.
        /// </summary>
        private void OnVideoPrepared(VideoPlayer vp)
        {
            vp.Play();
            if (audioSource != null) audioSource.Play();

            // Adjust aspect ratio if AspectRatioFitter is present
            if (aspectRatioFitter != null && vp.width > 0 && vp.height > 0)
            {
                aspectRatioFitter.aspectRatio = (float)vp.width / (float)vp.height;
            }

            string currentTitle = GetSelectionTitle(currentSelection);
            UpdateStatusUI("Playing", currentTitle);
        }

        /// <summary>
        /// Called when VideoPlayer encounters an error.
        /// </summary>
        private void OnVideoError(VideoPlayer vp, string message)
        {
            Debug.LogError($"[RaspberryPiVideoPlayer] Video Error: {message}");
            UpdateStatusUI("Playback Error", message);
        }

        /// <summary>
        /// Resolves absolute path from project relative path.
        /// </summary>
        private string ResolveFilePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            if (path.StartsWith("Assets/"))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, path);
            }

            return Path.Combine(Application.streamingAssetsPath, path);
        }

        private string GetSelectionTitle(VideoSelection selection)
        {
            switch (selection)
            {
                case VideoSelection.VideoA: return titleA;
                case VideoSelection.VideoB: return titleB;
                case VideoSelection.VideoC: return titleC;
                default: return "No Video Selected";
            }
        }

        private void UpdateStatusUI(string state, string title)
        {
            if (statusText != null)
            {
                if (string.IsNullOrEmpty(title))
                {
                    statusText.text = state;
                }
                else
                {
                    statusText.text = $"<b>[{state.ToUpper()}]</b> {title}";
                }
            }
        }

        private void UpdateInstructionsUI()
        {
            if (instructionsText != null)
            {
                instructionsText.text = "<b>Controls:</b>  [<b>A</b>] Video 1  |  [<b>B</b>] Video 2  |  [<b>C</b>] Video 3  |  [<b>SPACE</b>] Pause/Play  |  [<b>S</b>] Stop";
            }
        }
    }
}
