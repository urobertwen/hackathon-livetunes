using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class CubeSpawner : MonoBehaviour
{
    public GameObject cubePrefab; // Reference to the cube prefab
    public int numberOfCubes = 10000; // Number of cubes to spawn
    public float minBounceHeight = 0f; // Minimum bounce height of the cubes
    public float maxBounceHeight = 5f; // Maximum bounce height of the cubes
    public float minBounceSpeed = 0f; // Minimum bounce speed of the cubes
    public float maxBounceSpeed = 5f; // Maximum bounce speed of the cubes

    private float deltaTime = 0.0f;
    private float[] cubeTargets; // Array to store the target heights for each cube
    private float[] cubeSpeeds; // Array to store the bounce speeds for each cube
    private int fps;
    bool hasRun = false; 

    public struct userAttributes {}
    public struct appAttributes 
    { 
        public int currentFPS { get; set; }
    }

    async Task InitializeRemoteConfigAsync()
    {
            // initialize handlers for unity game services
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
    }

    async Task Start()
    {
        await InitializeRemoteConfigAsync();
        RemoteConfigService.Instance.FetchCompleted += ApplyRemoteSettings;
        cubeTargets = new float[numberOfCubes];
        cubeSpeeds = new float[numberOfCubes];
        SetCubeTargets();
        SetCubeSpeeds();
        SpawnCubes();
    }

    void ApplyRemoteSettings(ConfigResponse configResponse)
    {
        numberOfCubes = RemoteConfigService.Instance.appConfig.GetInt("numberOfCubes",numberOfCubes);
        Debug.Log("RemoteConfigService.Instance.appConfig fetched: " + RemoteConfigService.Instance.appConfig.config.ToString());

        //clear the cubes from the scene
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        SetCubeTargets();
        SetCubeSpeeds();
        SpawnCubes();
    }

    async Task Update()
    {
        BounceCubes();
        CalculateFPS();
        await Task.Delay(1000);

        if (!hasRun)
        {
            var attributes = new appAttributes();
            attributes.currentFPS = fps;
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), attributes);
            hasRun = true;
        }

    }

    void SetCubeTargets()
    {
        for (int i = 0; i < numberOfCubes; i++)
        {
            cubeTargets[i] = Random.Range(minBounceHeight, maxBounceHeight);
        }
    }

    void SetCubeSpeeds()
    {
        for (int i = 0; i < numberOfCubes; i++)
        {
            cubeSpeeds[i] = Random.Range(minBounceSpeed, maxBounceSpeed);
        }
    }

    void SpawnCubes()
    {
        
        for (int i = 0; i < numberOfCubes; i++)
        {
            Vector3 spawnPosition = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
            GameObject cube = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);
            cube.transform.parent = transform; // Set parent to this object for organization

            // Random color for each cube
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.material.color = new Color(Random.value, Random.value, Random.value);
        }
    }

    void BounceCubes()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            float newY = Mathf.PingPong(Time.time * cubeSpeeds[i], cubeTargets[i]);
            child.position = new Vector3(child.position.x, newY, child.position.z);
        }
    }

    void CalculateFPS()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        fps = Mathf.RoundToInt(1.0f / deltaTime);
    }

    void OnGUI()
    {
        int fps = Mathf.RoundToInt(1.0f / deltaTime);
        GUIStyle style = new GUIStyle();
        Rect rect = new Rect(10, 10, 150, 50);
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        // Set background color
        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dark gray color with some transparency
        // Draw a background box for labels
        GUI.Box(new Rect(5, 5, 240, 240), "");
        // Reset background color
        GUI.backgroundColor = Color.clear;

        // Display FPS
        GUI.Label(rect, "FPS: " + fps, style);

        // Display numberOfCubes, minBounceHeight, maxBounceHeight, minBounceSpeed, and maxBounceSpeed
        Rect rect2 = new Rect(10, 60, 250, 50);
        GUI.Label(rect2, "Number of Cubes: " + numberOfCubes.ToString(), style);

        Rect rect3 = new Rect(10, 90, 250, 50);
        GUI.Label(rect3, "Min Bounce Height: " + minBounceHeight.ToString(), style);

        Rect rect4 = new Rect(10, 120, 250, 50);
        GUI.Label(rect4, "Max Bounce Height: " + maxBounceHeight.ToString(), style);

        Rect rect5 = new Rect(10, 150, 250, 50);
        GUI.Label(rect5, "Min Bounce Speed: " + minBounceSpeed.ToString(), style);

        Rect rect6 = new Rect(10, 180, 250, 50);
        GUI.Label(rect6, "Max Bounce Speed: " + maxBounceSpeed.ToString(), style);
    }
}