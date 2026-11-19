Coloca aquí el modelo ONNX de MoveNet / PoseNet.

Ejemplo de nombre:
  movenet_singlepose_lightning_4.onnx

Tras importar, Unity + Barracuda generará un asset NNModel.
Asígnarlo en el componente BodyPoseDetector → Model Asset.

Worker recomendado: ComputePrecompiled (GPU).
Input size Lightning: 192
Input size Thunder: 256

Sin modelo, el juego usa modo teclado (WASD).
