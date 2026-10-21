using UnityEngine;

public class TerrainPainter6 : MonoBehaviour
{
    public MeshRenderer terrainRenderer;
    public Texture2D[] brushTextures; // Array de texturas de pincel
    [Range(0, 4)]
    public int brushIndex = 0; // Índice de la textura de pincel actual
    public Texture2D maskTexture;
    [Range(0.0f, 1.0f)]
    public float brushOpacity = 1.0f;
    [Range(0.0f, 360.0f)]
    public float brushRotation = 0.0f;
    [Range(1, 100)]
    public int brushSize = 10;

    private void Awake()
    {
        // Obtener el componente MeshRenderer del objeto Terrain_Geo
        terrainRenderer = GetComponent<MeshRenderer>();
    }

    public void Update()
    {
        if (Input.GetMouseButton(0))
        {
            // Obtener las coordenadas del click de ratón en el plano del objeto Terrain_Geo
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit))
            {
                Vector2 pixelUV = hit.textureCoord;

                // Obtener la posición de la textura correspondiente al pixel en el que se hizo clic
                Texture2D texture = (Texture2D)terrainRenderer.material.mainTexture;
                int textureX = (int)(pixelUV.x * texture.width);
                int textureY = (int)(pixelUV.y * texture.height);

                // Obtener la textura de pincel actual
                Texture2D brushTexture = brushTextures[brushIndex];

                // Dibujar la textura de pincel en la textura del terreno
                Color[] brushPixels = brushTexture.GetPixels();
                int brushWidth = brushTexture.width;
                int brushHeight = brushTexture.height;
                int brushSize = this.brushSize;
                float brushScale = brushSize / (float)brushTexture.width;
                for (int y = 0; y < brushHeight; y++)
                {
                    for (int x = 0; x < brushWidth; x++)
                    {
                        Color brushPixel = brushPixels[y * brushWidth + x];
                        brushPixel.a *= brushOpacity;
                        brushPixel.a = Mathf.Clamp01(brushPixel.a);

                        if (brushPixel.a == 0)
                        {
                            continue;
                        }

                        Vector2 brushPos = new Vector2(x, y);
                        brushPos -= new Vector2(brushWidth, brushHeight) * 0.5f;
                        brushPos = RotateVector2(brushPos, brushRotation);
                        brushPos += new Vector2(brushWidth, brushHeight) * 0.5f;
                        brushPos *= brushScale;
                        int texCoordX = textureX + (int)brushPos.x - brushSize / 2;
                        int texCoordY = textureY + (int)brushPos.y - brushSize / 2;

                        if (texCoordX < 0 || texCoordX >= texture.width || texCoordY < 0 || texCoordY >= texture.height)
                        {
                            continue;
                        }

                        Color texel = texture.GetPixel(texCoordX, texCoordY);
                        texel *= (1.0f - brushPixel.a);
                        texel += brushPixel * brushPixel.a;
                        // Aplicar la máscara si está presente
                        if (maskTexture != null)
                        {
                            Color maskPixel = maskTexture.GetPixel(textureX, textureY);
                            texel *= maskPixel;
                        }

                        texture.SetPixel(texCoordX, texCoordY, texel);
                        texture.Apply();
                    }
                }
                //texture.Apply();
            }
        }
    }
    private Vector2 RotateVector2(Vector2 vector, float angle)
    {
        float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
        float cos = Mathf.Cos(angle * Mathf.Deg2Rad);

        float tx = vector.x;
        float ty = vector.y;
        float x = vector.x * cos - vector.y * sin;
        float y = vector.x * sin + vector.y * cos;
        return new Vector2(x, y);
    }
}
