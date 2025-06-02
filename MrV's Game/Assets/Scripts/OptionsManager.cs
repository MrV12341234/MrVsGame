using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OptionsManager : MonoBehaviour
{
    [Header("Sensitivity Options")]
    public TextMeshProUGUI sensitivityHeading;
    public Slider sensitivitySlider;
    
    [Header("FOV Options")]
    public TextMeshProUGUI FOVHeading;
    public Slider FOVSlider;
    
    [Header("Resolution Options")]
    public TextMeshProUGUI resolutionHeading;
    public Slider resolutionSlider;
    
    [Header("Quality Level")]
    public TextMeshProUGUI currentQualityLevel;

    [Header("Fullscreen Mode")] public Toggle fullscreenToggle;

    private float defaultSensitivity = 1f; // sensitivity settings are .05 to 2
    private float defaultFOV = 70f;
    private float defaultResolution = 1f;
    private int defaultQualityLevel = 0;
    private int defaultFullscreenState = 1; // 1= fullscreen, 0 = not fullscreen
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        float savedSens = PlayerPrefs.GetFloat("savedSens", defaultSensitivity);
        float savedFOV = PlayerPrefs.GetFloat("savedFOV", defaultFOV);
        float savedRes = PlayerPrefs.GetFloat("savedRes", defaultResolution);
        int savedQuality = PlayerPrefs.GetInt("savedQuality", defaultQualityLevel);
        int savedFullscreenState = PlayerPrefs.GetInt("savedFullscreenState", defaultFullscreenState);
        
        sensitivitySlider.value = savedSens;
        UpdateSensitivity(savedSens);
        
        FOVSlider.value = savedFOV;
        UpdateFOV(savedFOV);
        
        resolutionSlider.value = savedRes;
        UpdateResolution(savedRes);
        
        SetQualityLevel(savedQuality);

        fullscreenToggle.isOn = savedFullscreenState == 1;
        UpdateFullScreenMode(savedFullscreenState == 1);
    }

    public void UpdateResolution(float value)
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset != null) asset.renderScale = value;
        
        // converts resolution value from large decimal to a percentage with 2 decimals
        resolutionHeading.text = "Resolution: " + (value * 100).ToString("F2") + "%";
        // saves player resolution value
        PlayerPrefs.SetFloat("savedRes", value);
        
    }

    public void UpdateSensitivity(float value)
    {
        PlayerPrefs.SetFloat("savedSens", value);
        sensitivityHeading.text = "Sensitivity: " + value.ToString("F2");
    }

    public void UpdateFullScreenMode(bool mode)
    {
        Screen.fullScreenMode = mode ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
        PlayerPrefs.SetInt("savedFullscreenState", mode ? 1 : 0);
    }
    
    public void UpdateFOV(float value)
    {
        PlayerPrefs.SetFloat("savedFOV", value);
        FOVHeading.text = "FOV: " + value.ToString("F2");
    } 
    
    #region Updating Quality Levels
     public void SetPreviousQualityLevel()
        {
            int current = QualitySettings.GetQualityLevel();
            current--;
    
            if (current < 0) current = QualitySettings.names.Length - 1;
            SetQualityLevel(current);
            
        }
    
        private void SetQualityLevel(int current)
        {
            QualitySettings.SetQualityLevel(current);
                    
            PlayerPrefs.SetInt("savedQuality", current);
            currentQualityLevel.text = QualitySettings.names[current];
            
            float savedRes = PlayerPrefs.GetFloat("savedRes", defaultResolution);
            UpdateResolution(savedRes);
        }
    
        public void SetNextQualityLevel()
        {
            int current = QualitySettings.GetQualityLevel();
            current++;
            if (current > QualitySettings.names.Length - 1) current = 0; 
            SetQualityLevel(current);
        }
    #endregion
   

    // Update is called once per frame
    void Update()
    {
        
    }
}
