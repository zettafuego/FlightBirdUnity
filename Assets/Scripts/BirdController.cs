// Flight Bird - Motion Control
// BirdController.cs — Movimiento del pájaro por pose corporal (o teclado).
using UnityEngine;

/// <summary>
/// Controla el Rigidbody del pájaro:
/// - Altura de brazos → elevación (Y)
/// - Inclinación hombros → dirección (X)
/// - Distancia entre brazos → velocidad
/// Suavizado con Lerp (factor 10).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BirdController : MonoBehaviour
{
    [Header("Referencias")]
    public BodyPoseDetector pose;
    public TrailRenderer trail;
    public ParticleSystem wingParticles;

    [Header("Límites de vuelo")]
    public float minY = 5f;
    public float maxY = 80f;
    public float lateralLimit = 40f;

    [Header("Movimiento")]
    public float baseForwardSpeed = 18f;
    public float maxForwardSpeed = 45f;
    public float verticalSpeed = 22f;
    public float lateralSpeed = 28f;
    public float rotateLerp = 8f;
    [Tooltip("Suavizado Vector3.Lerp del movimiento (requisito: 10f).")]
    public float moveLerp = 10f;

    [Header("Estado")]
    public bool inputEnabled = true;

    Rigidbody _rb;
    Vector3 _velocity;
    Vector3 _targetPos;
    float _yaw;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _targetPos = transform.position;
    }

    void Start()
    {
        if (pose == null) pose = BodyPoseDetector.Instance;
        if (trail == null) trail = GetComponentInChildren<TrailRenderer>();
        if (wingParticles == null) wingParticles = GetComponentInChildren<ParticleSystem>();
    }

    void FixedUpdate()
    {
        if (!inputEnabled || pose == null)
        {
            _rb.velocity = Vector3.zero;
            return;
        }

        // Señales 0..1 / -1..1
        float height01 = pose.ArmHeight01;
        float lean = pose.ShoulderLean01;
        float width01 = pose.ArmWidth01;

        float targetY = Mathf.Lerp(minY, maxY, height01);
        float targetX = Mathf.Clamp(transform.position.x + lean * lateralSpeed * Time.fixedDeltaTime * 3f, -lateralLimit, lateralLimit);
        float speed = Mathf.Lerp(baseForwardSpeed, maxForwardSpeed, width01);

        Vector3 current = transform.position;
        Vector3 desired = new Vector3(targetX, targetY, current.z + speed * Time.fixedDeltaTime);

        // Suavizado Lerp factor 10
        _targetPos = Vector3.Lerp(current, desired, moveLerp * Time.fixedDeltaTime);
        // Mantener avance en Z
        _targetPos.z = current.z + speed * Time.fixedDeltaTime;

        Vector3 vel = (_targetPos - current) / Time.fixedDeltaTime;
        // Limitar vertical
        vel.y = Mathf.Clamp(vel.y, -verticalSpeed, verticalSpeed);
        _rb.velocity = vel;

        // Orientación del pájaro
        float pitch = Mathf.Clamp(-vel.y * 2f, -25f, 25f);
        float roll = Mathf.Clamp(-lean * 35f, -40f, 40f);
        _yaw = Mathf.Lerp(_yaw, lean * 15f, rotateLerp * Time.fixedDeltaTime);
        Quaternion targetRot = Quaternion.Euler(pitch, _yaw, roll);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateLerp * Time.fixedDeltaTime);

        // FX
        if (wingParticles != null)
        {
            var em = wingParticles.emission;
            em.rateOverTime = Mathf.Lerp(5f, 40f, width01);
        }
        if (trail != null)
            trail.emitting = speed > baseForwardSpeed * 0.9f;
    }

    public void ResetBird(Vector3 pos)
    {
        transform.position = pos;
        _targetPos = pos;
        _rb.velocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
        _yaw = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Obstacle") || collision.collider.CompareTag("Terrain"))
        {
            GameManager.Instance?.OnBirdHitObstacle();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fruit"))
        {
            var fruit = other.GetComponent<FruitCollectible>();
            if (fruit != null) fruit.Collect();
        }
    }
}
