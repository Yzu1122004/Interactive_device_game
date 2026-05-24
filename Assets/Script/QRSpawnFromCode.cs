using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;
using System.Collections.Generic;


public class QRSpawnFromCode : MonoBehaviour
{
    public RawImage cameraPreview;

    public GameObject plantPrefab;
    public GameObject trafficConePrefab;
    public GameObject tirePrefab;
    public GameObject shopPrefab;
    public GameObject ironBucketPrefab;
    public GameObject motorPrefab;

    private WebCamTexture webcamTexture;
    private BarcodeReader<byte[]> barcodeReader;
    private bool hasScanned = false;

    void Start()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("No webcam found!");
            return;
        }

        string cameraName = WebCamTexture.devices[0].name;
        webcamTexture = new WebCamTexture(cameraName, 1280, 720);

        cameraPreview.texture = webcamTexture;
        cameraPreview.material.mainTexture = webcamTexture;
        cameraPreview.color = Color.white;

        webcamTexture.Play();

        barcodeReader = new BarcodeReader<byte[]>(
            (bytes) => new RGBLuminanceSource(
                bytes,
                webcamTexture.width,
                webcamTexture.height,
                RGBLuminanceSource.BitmapFormat.RGBA32
            )
        );

        barcodeReader.Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new List<BarcodeFormat>
            {
                BarcodeFormat.QR_CODE
            }
        };

        barcodeReader.AutoRotate = true;
        barcodeReader.TryInverted = true;

    }

    void Update()
    {
        if (hasScanned) return;
        if (webcamTexture == null) return;
        if (!webcamTexture.isPlaying) return;
        if (webcamTexture.width <= 16) return;

        try
        {
            Color32[] pixels = webcamTexture.GetPixels32();
            byte[] rawBytes = new byte[pixels.Length * 4];

            for (int i = 0; i < pixels.Length; i++)
            {
                rawBytes[i * 4] = pixels[i].r;
                rawBytes[i * 4 + 1] = pixels[i].g;
                rawBytes[i * 4 + 2] = pixels[i].b;
                rawBytes[i * 4 + 3] = pixels[i].a;
            }

            var result = barcodeReader.Decode(rawBytes);

            if (result == null)
            {
                Debug.Log("Scanning but no QR detected..");
            }

            if (result != null)
            {
                Debug.Log("QR Data: " + result.Text);

                SpawnObjectFromQR(result.Text);

                StartCoroutine(ResetScan());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("QR scan error: " + e.Message);
        }
    }

    public void SpawnObjectFromQR(string qrText)
    {
        Debug.Log("RAW = [" + qrText + "]");
        qrText = qrText.Trim();

        Debug.Log("Raw QR Text: [" + qrText + "]");
        Debug.Log("QR TEXT LENGTH = " + qrText.Length);
        string[] data = qrText.Split(',');

        Debug.Log("DATA LENGTH = " + data.Length);
        if (data.Length < 3)
        {
            Debug.LogError("Wrong QR format. Use: type,x,z");
            return;
        }

        int type;
        float x;
        float z;

        if (!int.TryParse(data[0].Trim(), out type))
        {
            Debug.LogError("Wrong type value: " + data[0]);
            return;
        }

        if (!float.TryParse(data[1].Trim(), out x))
        {
            Debug.LogError("Wrong X value: " + data[1]);
            return;
        }

        if (!float.TryParse(data[2].Trim(), out z))
        {
            Debug.LogError("Wrong Z value: " + data[2]);
            return;
        }

        GameObject prefab = GetPrefabByType(type);

        if (prefab == null)
        {
            Debug.LogError("No prefab found for type: " + type);
            return;
        }

        Vector3 spawnPosition = new Vector3(x, 0, z);
        Instantiate(prefab, spawnPosition, Quaternion.identity);

        Debug.Log("Spawned type " + type + " at X=" + x + ", Z=" + z);
    }

    GameObject GetPrefabByType(int type)
    {
        if (type == 1) return plantPrefab;
        if (type == 2) return trafficConePrefab;
        if (type == 3) return tirePrefab;
        if (type == 4) return shopPrefab;
        if (type == 5) return ironBucketPrefab;
        if (type == 6) return motorPrefab;

        return null;
    }

    System.Collections.IEnumerator ResetScan()
    {
        hasScanned = true;
        yield return new WaitForSeconds(2f);
        hasScanned = false;
    }
}