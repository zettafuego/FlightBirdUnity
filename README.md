# Flight Bird - Motion Control

Juego 3D Unity (URP) controlado por pose corporal vía webcam y Unity Barracuda (MoveNet o PoseNet ONNX).

---

## Requisitos

| Item | Versión |
|------|---------|
| Unity | **2021.3 LTS** o superior |
| Pipeline | **URP** (Universal Render Pipeline) |
| ML | **com.unity.barracuda** 3.x |
| UI | TextMeshPro |
| Hardware | Webcam + GPU con Compute Shaders (recomendado) |

---

## Scripts incluidos

| Script | Rol |
|--------|-----|
| `WebcamManager.cs` | Webcam 640×480 / 1280×720 + fallback teclado |
| `BodyPoseDetector.cs` | Barracuda + 17 keypoints + señales de brazos |
| `SkeletonVisualizer.cs` | Esqueleto cian / puntos amarillos en UI |
| `BirdController.cs` | Rigidbody, Lerp 10, trail + partículas |
| `GameManager.cs` | Estados, 16 frutas, timer REC, victoria |
| `FruitCollector.cs` | Pool de manzanas + spawn procedural |
| `ProceduralTerrain.cs` | Terreno infinito low-poly + pooling |
| `CameraFollow.cs` | Cámara 3ª persona (en mismo archivo terreno) |
| `UIManager.cs` | Score 🍎, timer, status, paneles |
| `BootstrapScene.cs` | Genera escena jugable mínima al Start |

---

## Arranque rápido (5 minutos)

1. Abre **Unity Hub → New project → 3D (URP)** o **Open** esta carpeta si ya es un proyecto Unity.
2. Si abriste proyecto vacío URP: copia la carpeta `Assets` y `Packages/manifest.json` de este repo.
3. **Window → Package Manager**: confirma **Barracuda** y **URP**.
4. Crea escena vacía `Assets/Scenes/Main.unity`.
5. Crea GameObject vacío `Bootstrap` → añade `BootstrapScene` → Play.
6. Controles de prueba **sin cámara**:
   - **W/S** o ↑↓ : subir / bajar  
   - **A/D** o ←→ : girar  
   - **Q/E** : cerrar / abrir brazos (velocidad)  
   - **Espacio** : saltar calibración  
   - **Esc** : pausa  

7. Para **ML de verdad**: importa modelo ONNX (ver abajo) y asígnalo en `BodyPoseDetector.modelAsset`.

---

## Modelo ML (MoveNet)

### Opción recomendada: MoveNet Lightning ONNX

1. Descarga un ONNX de MoveNet SinglePose Lightning, por ejemplo desde:
   - [TensorFlow Hub MoveNet](https://tfhub.dev/google/movenet/singlepose/lightning/4) (exportar/convertir a ONNX), o
   - Repositorios comunitarios “movenet lightning onnx”.
2. Copia el archivo a `Assets/ML/movenet_lightning.onnx`.
3. Unity + Barracuda lo importa como **NN Model**.
4. Selecciona el GameObject con `BodyPoseDetector` → arrastra el asset a **Model Asset**.
5. Ajusta `inputSize` (192 Lightning / 256 Thunder).
6. `workerType` = **ComputePrecompiled** (GPU) o **CSharpBurst** si no hay compute.

### Entrada / salida esperadas

- **Input:** tensor imagen `1×3×H×W` o NHWC según el ONNX (Barracuda adapta en muchos casos).
- **Output:** 17 puntos × (y, x, score) normalizados 0–1.

Si el nombre del tensor de entrada no es `input`, edita en `BodyPoseDetector.RunInference` el `inputName` leyendo `_runtimeModel.inputs[0].name` (ya implementado).

---

## Jerarquía de escena recomendada

```
Main
├── Main Camera (+ CameraFollow, AudioListener)
├── Directional Light
├── Managers
│   ├── WebcamManager
│   ├── BodyPoseDetector
│   ├── GameManager
│   ├── FruitCollector
│   └── ProceduralTerrain
├── Environment (generado en runtime)
├── Player_Bird
│   ├── Mesh / Rig
│   ├── TrailRenderer
│   └── ParticleSystem
├── Collectibles (pool)
└── UI_Canvas
    ├── Score (TMP)  — Upper Left  "🍎 X/16"
    ├── Timer (TMP)  — Upper Right "REC MM:SS"
    ├── Status
    ├── WebcamOverlay (RawImage + SkeletonVisualizer)
    └── VictoryPanel
```

O usa solo **BootstrapScene** y luego organiza.

---

## Tags y Layers

### Tags (Edit → Project Settings → Tags and Layers)

| Tag | Uso |
|-----|-----|
| `Player` | Pájaro |
| `Fruit` | Manzanas (trigger) |
| `Obstacle` | Montañas / obstáculos |
| `Terrain` | Suelo |

### Layers sugeridos

| Layer | Uso |
|-------|-----|
| Default | General |
| Player | Pájaro |
| Fruit | Collectibles |
| Environment | Terreno |
| UI | Canvas |

Physics: Player ↔ Fruit = trigger; Player ↔ Obstacle = colisión.

---

## Player Settings (resumen)

| Setting | Valor |
|---------|--------|
| Company Name | UNCP / tu estudio |
| Product Name | Flight Bird Motion Control |
| Resolution | 1920×1080 fullscreen / windowed |
| Target Frame Rate | 60 (código: `Application.targetFrameRate = 60`) |
| Color Space | **Linear** (URP) |
| Graphics APIs Windows | DX11 / DX12 |
| Android | OpenGL ES 3.0+, ARM64 |
| WebGL | WebGL 2.0 (cámara con limitaciones) |
| Active Input Handling | Input Manager (Old) o Both |

---

## Gameplay (resumen)

1. El jugador se coloca frente a la webcam.  
2. **3 s de calibración** (mover brazos arriba/abajo y abrir/cerrar).  
3. Brazos **arriba** → sube; **abajo** → baja.  
4. **Inclinar** cuerpo / brazos → girar.  
5. Brazos **abiertos** → más velocidad.  
6. Recolectar **16 manzanas** → victoria + tiempo REC.  
7. Colisión con montaña → empujón atrás.  
8. Reiniciar desde UI.

---

## Assets a añadir (lista)

| Asset | Notas |
|-------|--------|
| Pájaro low-poly | Capsule placeholder en Bootstrap; ideal: mesh con material URP Lit |
| Manzana | Prefab esfera roja o modelo + rotación (FruitCollectible) |
| Montañas / árboles | Generados proceduralmente; puedes sustituir por prefabs |
| Agua | Material URP color cian brillante |
| Skybox | Gradient o Procedural Sky URP |
| Audio | BGM loop, collect, hit, wind, win |
| UI | TMP default o fuente pixel (import Font → TMP Font Asset) |
| ML | `Assets/ML/*.onnx` |

---

## Optimización

- Barracuda: **ComputePrecompiled** / GPUCompute.  
- Inferencia ~30 FPS (`inferenceInterval = 1/30`).  
- Object pooling: frutas + segmentos de terreno.  
- Evitar `GetPixels` cada frame si no hay `didUpdateThisFrame` (ya filtrado).  
- No allocar en `Update` del visualizador (pool de UI Images creado una vez).  
- LOD / Occlusion: activar en modelos finales y Window → Rendering → Occlusion Culling.

---

## Fine-tuning (valores de partida)

| Parámetro | Valor inicial |
|-----------|----------------|
| Bird `moveLerp` | 10 |
| Bird `baseForwardSpeed` | 18 |
| Bird `maxForwardSpeed` | 45 |
| Bird `minY` / `maxY` | 5 / 80 |
| Pose `minKeypointScore` | 0.15 |
| Pose `inputSize` | 192 |
| Fruit `fruitsToWin` | 16 |
| Terrain `segmentLength` | 80 |

---

## Plataformas

| Plataforma | Notas |
|------------|--------|
| Windows | Mejor rendimiento Barracuda GPU |
| macOS | Metal; Compute según GPU |
| Android | ARM64, permisos CAMERA en manifest |
| WebGL | Webcam con HTTPS; Barracuda limitado — preferir teclado o WASM CPU |

Android `AndroidManifest` (permiso):

```xml
<uses-permission android:name="android.permission.CAMERA" />
```

---

## Estructura de carpetas

```
FlightBird-MotionControl/
├── Assets/
│   ├── Scripts/          ← todos los .cs
│   ├── ML/               ← coloca aquí el .onnx
│   ├── Scenes/
│   ├── Prefabs/
│   ├── Materials/
│   ├── Audio/
│   └── UI/
├── Packages/manifest.json
├── Docs/SETUP_ES.md
└── README.md
```

---

## Licencia / créditos

Proyecto plantilla educativa. MoveNet © Google. Unity © Unity Technologies.  
Adapta assets y modelo según sus licencias.

---

## Soporte rápido de errores

| Problema | Solución |
|----------|----------|
| Sin tracking | Asignar NNModel; mirar consola Barracuda |
| Negro en webcam | Permisos OS; probar otra cámara |
| Bajo FPS | Bajar `preferredWidth` a 640; `inputSize` 192; menos árboles |
| Frutas no se recogen | Tag `Fruit` + Collider **Is Trigger** en manzana y en pájaro un Rigidbody no-kinematic o trigger |
| Pájaro no se mueve | `inputEnabled` y calibración terminada; o teclado WASD |
