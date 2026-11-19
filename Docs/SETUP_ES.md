# Flight Bird - Motion Control — Guía de configuración (ES)

## 1. Crear / abrir el proyecto

1. Unity Hub → **Unity 2021.3 LTS** (o 2022 LTS).
2. **Add** → selecciona la carpeta  
   `Documents\1. Programación\FlightBird-MotionControl`  
   **o** crea un proyecto **3D (URP)** y copia dentro `Assets/` y `Packages/manifest.json`.
3. Espera a que Package Manager resuelva **URP** y **Barracuda**.

Si Barracuda no aparece:  
**Window → Package Manager → + → Add package by name** → `com.unity.barracuda`

---

## 2. Escena mínima (sin assets artísticos)

1. **File → New Scene** (Basic / URP).
2. Crea un GameObject vacío: `Bootstrap`.
3. Añade el componente **`BootstrapScene`**.
4. Pulsa **Play**.

Deberías ver:

- Terreno procedural con montañas/árboles/agua  
- Pájaro cápsula con estela  
- UI: 🍎 0/16, REC 00:00, overlay webcam  
- Control con **WASD** si no hay cámara  

---

## 3. Tags y Physics

**Edit → Project Settings → Tags and Layers**

Crea tags:

- `Player`
- `Fruit`
- `Obstacle`
- `Terrain`

**Physics**: deja Default. Las manzanas usan **Is Trigger**.  
El pájaro debe tener **Rigidbody** (no kinematic) o un collider no-trigger que dispare `OnTriggerEnter` con frutas trigger.

---

## 4. Modelo ML (MoveNet)

1. Obtén un ONNX **MoveNet SinglePose Lightning** (192×192).
2. Colócalo en `Assets/ML/`.
3. Unity genera un asset **NN Model**.
4. En la jerarquía: `Managers` → `BodyPoseDetector`:
   - **Model Asset** = tu NN Model  
   - **Input Size** = 192  
   - **Worker Type** = Compute Precompiled  
   - **Inference Interval** = 0.033  

### Si el ONNX falla al importar

- Reexporta con opset 9–12.  
- Prueba Worker **CSharpBurst**.  
- Revisa el nombre del tensor de entrada en la consola Barracuda.

---

## 5. Webcam

1. `WebcamManager` en Managers.  
2. **Preferred Width/Height**: 1280×720 (o 640×480 en portátiles).  
3. Asigna el **RawImage** del overlay (Bootstrap lo hace solo).  
4. En Windows: permite cámara a Unity en Privacidad del sistema.

**Fallback teclado** (si no hay cámara):

| Tecla | Acción |
|-------|--------|
| W / ↑ | Subir |
| S / ↓ | Bajar |
| A / ← | Izquierda |
| D / → | Derecha |
| Q / E | Menos / más velocidad (ancho de brazos) |
| Espacio | Saltar calibración |
| Esc | Pausa |

---

## 6. UI (TextMeshPro)

1. Primera vez: **Import TMP Essentials**.  
2. Textos:
   - Superior izquierda: `🍎 0/16`  
   - Superior derecha: `REC MM:SS`  
3. Fuente: TMP default o pixel art (Window → TextMeshPro → Font Asset Creator).

---

## 7. Audio

En `GameManager`:

- `musicSource` → clip BGM (loop)  
- `sfxCollect`, `sfxHit`, `sfxWin`  

Crea un `AudioSource` en Managers si hace falta.

---

## 8. Build

### Windows

1. File → Build Settings → PC, Mac & Linux Standalone → Windows x86_64.  
2. Player Settings → Fullscreen 1920×1080, Linear color.  
3. Build.

### Android

1. Switch Platform → Android.  
2. IL2CPP + ARM64.  
3. Custom Main Manifest con `CAMERA`.  
4. Minimum API 24+.

### WebGL

1. Barracuda en CPU (lento).  
2. Servir por HTTPS para getUserMedia.  
3. Considera solo modo teclado para demos web.

---

## 9. Capas de colisión (opcional)

| Layer | Collide with |
|-------|----------------|
| Player | Obstacle, Terrain, Fruit |
| Fruit | Player only |
| Environment | Player |

---

## 10. Checklist de calidad

- [ ] 60 FPS en editor con Stats  
- [ ] Inferencia ≤ 16–33 ms  
- [ ] 16 frutas y victoria  
- [ ] Skeleton visible sobre webcam  
- [ ] Sin GC spikes (Profiler)  
- [ ] Fallback teclado funciona  

---

## 11. Ajuste fino

| Sensación | Sube | Baja |
|-----------|------|------|
| Respuesta vertical | `verticalSpeed`, rango calibración | `moveLerp` |
| Giros | `lateralSpeed` | lean deadzone (código) |
| Velocidad punta | `maxForwardSpeed` | `baseForwardSpeed` |
| Tracking ruidoso | `SmoothSpeed` en detector | `minKeypointScore` |

---

## Soporte

Revisa la consola Unity (`BodyPoseDetector`, `WebcamManager`, `Barracuda`).  
Sin modelo NN el juego sigue siendo jugable con teclado.
