<header>

![406096682-23848ed1-4205-421e-89df-7a7066139400](https://github.com/user-attachments/assets/9bdc4de8-b22f-4e40-9323-8b502ad9f7e9)

# **FootPrints**

_**Guía visual con huellas que marcan el camino a seguir. Útil en tutoriales o misiones dirigidas.**_


</header>
   
<footer>
   

## Requerimientos

---

[Cinemachine](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html)


## Escena de ejemplo Footprints RT

La escena **`Assets/3-Scenes/FootprintsRT.unity`** muestra la implementación con RenderTexture (opción A).

1. Abre la escena en Unity y presiona **Play**. Un caminante de prueba recorre un circuito circular y va pintando huellas en la máscara RT.
2. El objeto **Ground** tiene el componente `FootprintPainterRT`. Sus parámetros principales son:
   * **Tile Size / Origin**: definen el trozo del mundo que cubre la máscara.
   * **Resolution / Filter Mode**: controlan el tamaño del RenderTexture y cómo se suaviza.
   * **Follow Target**: (opcional) reposiciona el tile cuando el caminante se aleja.
   * **Fade**: atenúa gradualmente la máscara para que las huellas desaparezcan.
3. El shader del suelo (`GroundFootprintsRT` → `Universal Render Pipeline/Footprints/Ground`) lee la textura global `_FootMask` que configura el `FootprintPainterRT`.
4. Para configuraciones basadas en Shader Graph usa el componente `FootprintTerrainBinder` en el mismo `Renderer` del suelo (ya está en la escena). Este componente aplica la textura de máscara y el vector `_FootTileOriginSize` a través de un `MaterialPropertyBlock`, lo que permite que los gráficos lean la RT sin duplicar materiales.
5. El script `FootprintDemoWalker` (en el objeto **Walker**) llama a `FootprintPainterRT.Stamp` cada vez que toca un paso nuevo. Puedes reutilizarlo como referencia para alimentar la máscara desde tus propios personajes.

> Nota: si no asignas una textura de suela, `FootprintPainterRT` genera una en tiempo de ejecución para que el sistema funcione directamente.


</footer>
