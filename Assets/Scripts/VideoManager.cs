using UnityEngine;
using UnityEngine.Video;
using System.IO;

public class WebGLVideoFix : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string videoFileName = "StartScene.mp4"; // Make sure to include the file extension like .mp4 or .webm

    void Start()
    {
        // Mute the video to bypass strict browser autoplay policies
        videoPlayer.SetDirectAudioMute(0, true);

        // Change source to URL for better WebGL compatibility
        videoPlayer.source = VideoSource.Url;

        // Construct the correct path for the StreamingAssets folder
        string videoPath = Path.Combine(Application.streamingAssetsPath, videoFileName);
        
        // Assign the URL path
        videoPlayer.url = videoPath;

        // Prepare the video in the background before playing
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        // Play the video automatically once it's fully prepared and loaded
        vp.Play();
    }
}