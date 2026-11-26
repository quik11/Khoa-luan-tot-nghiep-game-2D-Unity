using UnityEngine;
using TMPro;

public class HealthText : MonoBehaviour
{
    [TenHienThi("Tốc bay lên")] public Vector3 moveSpeed = new Vector3(0,75,0);
    [TenHienThi("Thời gian mờ")] public float timeToFade= 1f;

    RectTransform textTransform;
    TextMeshProUGUI textMeshPro;
    private float timeElapsed;
    private Color startColor;
    private void Awake()
    {
        textTransform = GetComponent<RectTransform>(); 
        textMeshPro = GetComponent<TextMeshProUGUI>();
        GameFont.Apply(textMeshPro); // số bay sinh ra sau lúc vào màn nên phải tự đổi font
        startColor = textMeshPro.color;

    }

    private void Update()
    {
        textTransform.position += moveSpeed * Time.deltaTime;

        timeElapsed += Time.deltaTime; 
        if (timeElapsed < timeToFade)
        {
            float fadeAlpha = startColor.a * (1 - (timeElapsed / timeToFade));
            textMeshPro.color = new Color( startColor.r, startColor.g, startColor.b, fadeAlpha);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
