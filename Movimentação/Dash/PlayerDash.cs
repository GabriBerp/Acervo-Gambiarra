/*
 * ============================================================================
 *  PLAYER DASH (INVESTIDA RÁPIDA)  -  Versão Base
 * ============================================================================
 *
 *  CRÉDITOS
 *  --------
 *  Criado por: Acervo Gambiarra
 *  Este código é 100% GRATUITO. Use, modifique e distribua como quiser,
 *  em projetos pessoais ou comerciais.
 *  Se preferir, você pode APAGAR todos os blocos de comentário deste arquivo
 *  (incluindo este). Eles existem apenas para ajudar quem está começando.
 *  Feito por desenvolvedores brasileiros, para desenvolvedores brasileiros!
 *
 *  O QUE ESTE SCRIPT FAZ
 *  ---------------------
 *  Dá ao jogador uma investida rápida (dash) em uma direção. Durante o dash,
 *  este script assume o controle total da velocidade do Rigidbody e ignora
 *  a gravidade, e ao terminar devolve o controle. É independente: funciona
 *  sozinho, com qualquer script de movimento baseado em Rigidbody.
 *
 *  RECURSOS PRINCIPAIS
 *  -------------------
 *  - 3 modos de direção: para onde a câmera olha (inclusive para cima/
 *    baixo), para onde o jogador tenta ir (WASD) ou para frente do jogador.
 *  - Curva de velocidade (AnimationCurve): você desenha como a velocidade
 *    muda durante o dash (começa forte e acaba suave, constante, etc.).
 *  - Cooldown entre dashes e limite de dashes por "voo" (recarregam ao
 *    tocar o chão).
 *  - Cancela o dash ao bater em uma parede (opcional).
 *  - Velocidade de saída configurável (parar seco ou continuar deslizando).
 *  - Eventos (UnityEvent) ao iniciar e ao terminar.
 *  - Funciona com o Input System novo E com o antigo (Input Manager).
 *
 *  COMO USAR (PASSO A PASSO)
 *  -------------------------
 *  1. No objeto do jogador (precisa de Rigidbody e de um Collider),
 *     adicione este script.
 *  2. Em "Camera Transform" arraste a câmera (necessário para o modo
 *     CameraForward). Se deixar vazio, usa a Camera.main.
 *  3. Em "Ground Mask" escolha as layers de chão (tire a layer do próprio
 *     jogador). Isso serve só para recarregar os dashes ao tocar o chão.
 *  4. Ajuste velocidade, duração e cooldown. Dê Play e aperte Q!
 *
 *  PONTOS IMPORTANTES AO ADAPTAR PARA O SEU PROJETO
 *  ------------------------------------------------
 *  - Feito para Unity 6 ou mais nova (usa "linearVelocity"). Em versões
 *    antigas, troque por "velocity".
 *  - Durante o dash, o seu script de movimento NÃO pode mexer na
 *    velocidade do Rigidbody. Use "IsDashing" para pausá-lo:
 *        if (dash.IsDashing) return;
 *    Se usar o PlayerMovement deste pacote, isso já é automático.
 *  - A gravidade do Rigidbody (useGravity) é desligada durante o dash e
 *    restaurada no fim. Se o seu movimento aplica gravidade manual, pause-a
 *    também enquanto IsDashing for true.
 *  - Para usar seus próprios controles (celular, gamepad, rebind),
 *    desmarque "Use Built In Input" e chame TryDash() quando quiser. Para
 *    escolher a direção por código, use TryDash(Vector3 direction).
 *  - Quer um efeito de FOV ou câmera? Use "DashProgress01" (0 a 1) ou os
 *    eventos onDashStarted / onDashEnded.
 *  - Para voltar ao comportamento de "um dash até tocar o chão", deixe
 *    "Max Dashes" = 1 e "Refill On Ground" ligado.
 *
 *  COMUNICAÇÃO COM OUTROS SCRIPTS (OPCIONAL)
 *  -----------------------------------------
 *  Este script envia as mensagens OnPlayerDashStarted e OnPlayerDashEnded
 *  (SendMessage) para os outros scripts do mesmo objeto. O PlayerMovement e o
 *  PlayerSlide entendem essas mensagens sozinhos. Nenhum deles é obrigatório.
 *
 *  Obs.: o nome da classe precisa ser igual ao nome do arquivo (PlayerDash.cs).
 * ============================================================================
 */

using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
 * GUIA: DashDirectionMode
 * Define para onde o dash vai.
 *  - CameraForward: para onde a câmera olha.
 *  - MovementInput: para onde o jogador está tentando andar (WASD); sem
 *    input, vai para frente.
 *  - PlayerForward: sempre para a frente do jogador.
 */
public enum DashDirectionMode { CameraForward = 0, MovementInput = 1, PlayerForward = 2 }

/*
 * GUIA: PlayerDash
 * Classe principal do dash. Controla o início, a velocidade durante o dash
 * (seguindo a curva), o cooldown, o limite de dashes, o cancelamento por
 * colisão e o fim do dash.
 */
[RequireComponent(typeof(Rigidbody))]
public class PlayerDash : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("If false, this script reads no input. Call TryDash() from your own code.")]
    [SerializeField] private bool useBuiltInInput = true;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key dashKey = Key.Q;
#else
    [SerializeField] private KeyCode dashKey = KeyCode.Q;
#endif

    [Header("Direction")]
    [SerializeField] private DashDirectionMode directionMode = DashDirectionMode.CameraForward;
    [Tooltip("Camera used by CameraForward mode. Empty = Camera.main.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Reference for 'forward/right' in MovementInput mode. Empty = the player itself.")]
    [SerializeField] private Transform orientation;
    [Tooltip("CameraForward mode only: if true, looking up/down makes the dash go up/down.")]
    [SerializeField] private bool allowVerticalDash = true;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 30f;
    [SerializeField] private float dashDuration = 0.75f;
    [Tooltip("Speed multiplier over the dash. X = progress (0 to 1), Y = speed multiplier. Default: starts at full speed and fades to zero.")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("Fraction of the speed kept when the dash ends naturally. 0 = stops dead, 1 = keeps flying.")]
    [SerializeField, Range(0f, 1f)] private float exitVelocityMultiplier = 1f;

    [Header("Limits")]
    [Tooltip("Minimum time (seconds) between the start of one dash and the next.")]
    [SerializeField] private float dashCooldown = 1.5f;
    [Tooltip("How many dashes are allowed before touching the ground again. 0 = unlimited.")]
    [SerializeField] private int maxDashes = 1;
    [Tooltip("If true, touching the ground gives all dashes back.")]
    [SerializeField] private bool refillOnGround = true;

    [Header("Obstacles")]
    [Tooltip("If true, the dash stops when it hits a wall or obstacle.")]
    [SerializeField] private bool stopOnObstacle = true;
    [Tooltip("The dash stops when the dot product between the surface normal and the dash direction is below this value (-0.5 = hitting almost head-on).")]
    [SerializeField, Range(-1f, 0f)] private float obstacleDotThreshold = -0.5f;

    [Header("Ground Check (used to refill dashes)")]
    [Tooltip("Layers that count as ground. Remove the player's own layer from here.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Events")]
    public UnityEvent onDashStarted;
    public UnityEvent onDashEnded;

    // ---------- Internal state ----------

    private Rigidbody rb;
    private Collider col;

    private bool isDashing;
    private float dashElapsed;
    private float cooldownTimer;
    private int dashesUsed;
    private Vector3 dashDirection;
    private bool gravityWasEnabled;

    // ---------- Public API ----------

    /*
     * GUIA: Propriedades de leitura
     * IsDashing: true durante o dash (use para pausar seu movimento).
     * DashProgress01: 0 no início do dash e 1 no fim (ótimo para FOV e
     * efeitos). CanDash: true se um dash pode começar agora.
     * DashesRemaining: dashes restantes antes de tocar o chão
     * (int.MaxValue se ilimitado). CooldownRemaining: segundos até poder
     * usar de novo. DashDirection: direção do último dash.
     */
    public bool IsDashing => isDashing;
    public float DashProgress01 => isDashing ? Mathf.Clamp01(dashElapsed / dashDuration) : 0f;
    public bool CanDash => !isDashing && cooldownTimer <= 0f && (maxDashes <= 0 || dashesUsed < maxDashes);
    public int DashesRemaining => maxDashes <= 0 ? int.MaxValue : Mathf.Max(0, maxDashes - dashesUsed);
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
    public Vector3 DashDirection => dashDirection;

    // ---------- Unity messages ----------

    /*
     * GUIA: Awake
     * Pega o Rigidbody e o Collider do jogador. O Collider é usado apenas
     * para detectar o chão (recarga dos dashes).
     */
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    /*
     * GUIA: Update
     * Se "useBuiltInInput" estiver ativo, tenta iniciar o dash quando a
     * tecla for apertada.
     */
    private void Update()
    {
        if (useBuiltInInput && WasDashPressed())
        {
            TryDash();
        }
    }

    /*
     * GUIA: FixedUpdate
     * Ciclo da física. Diminui o cooldown e, se estiver em dash, avança o
     * tempo e aplica a velocidade (ou termina o dash). Fora do dash, devolve
     * os dashes quando o jogador toca o chão (se "refillOnGround" estiver
     * ligado).
     */
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (cooldownTimer > 0f) cooldownTimer -= dt;

        if (isDashing)
        {
            dashElapsed += dt;

            if (dashElapsed >= dashDuration)
            {
                EndDash(true);
            }
            else
            {
                ApplyDashVelocity();
            }
        }
        else if (refillOnGround && dashesUsed > 0 && IsGrounded())
        {
            dashesUsed = 0;
        }
    }

    /*
     * GUIA: OnDisable
     * Se o script for desligado no meio de um dash, encerra o dash para não
     * deixar a gravidade desligada ou outros scripts travados.
     */
    private void OnDisable()
    {
        if (isDashing) EndDash(false);
    }

    /*
     * GUIA: OnCollisionEnter / OnCollisionStay
     * Chamados pela Unity quando o jogador bate em algo. Repassam a colisão
     * para CheckObstacle, que decide se o dash deve parar.
     */
    private void OnCollisionEnter(Collision collision) { CheckObstacle(collision); }
    private void OnCollisionStay(Collision collision) { CheckObstacle(collision); }

    // ---------- Dash control (public) ----------

    /*
     * GUIA: TryDash
     * Tenta iniciar um dash usando o modo de direção configurado. Retorna
     * true se o dash começou e false se não foi possível (cooldown, sem
     * dashes sobrando ou já em dash). Pode ser chamado por qualquer script.
     */
    public bool TryDash()
    {
        if (!CanDash) return false;

        StartDash(ResolveDirection());
        return true;
    }

    /*
     * GUIA: TryDash (com direção)
     * Igual ao TryDash, mas você escolhe a direção por código (ex.: o
     * analógico do gamepad ou a direção de um inimigo). A direção é
     * normalizada automaticamente.
     */
    public bool TryDash(Vector3 direction)
    {
        if (!CanDash) return false;

        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = ResolveDirection();
        }

        StartDash(direction.normalized);
        return true;
    }

    /*
     * GUIA: CancelDash
     * Interrompe o dash imediatamente (sem aplicar a velocidade de saída).
     * Útil quando o jogador toma dano, morre ou entra em outro estado.
     */
    public void CancelDash()
    {
        if (isDashing) EndDash(false);
    }

    /*
     * GUIA: ResetDash
     * Devolve todos os dashes e zera o cooldown na hora. Use quando quiser
     * recarregar o dash por fora (ex.: ao pegar um item, ao soltar um
     * gancho, ao acertar um inimigo).
     */
    public void ResetDash()
    {
        dashesUsed = 0;
        cooldownTimer = 0f;
    }

    // ---------- Dash internals ----------

    /*
     * GUIA: StartDash
     * Inicia o dash: marca o estado, gasta um dash, inicia o cooldown, guarda
     * a direção, desliga a gravidade do Rigidbody (lembrando o valor
     * original) e já aplica a primeira velocidade, sem esperar o próximo
     * tick. Por fim, avisa os outros scripts (mensagem) e dispara o evento.
     */
    private void StartDash(Vector3 direction)
    {
        isDashing = true;
        dashElapsed = 0f;
        dashesUsed++;
        cooldownTimer = dashCooldown;
        dashDirection = direction;

        gravityWasEnabled = rb.useGravity;
        rb.useGravity = false;

        SendMessage("OnPlayerDashStarted", SendMessageOptions.DontRequireReceiver);
        onDashStarted?.Invoke();

        ApplyDashVelocity();
    }

    /*
     * GUIA: ApplyDashVelocity
     * Define a velocidade do Rigidbody para este instante do dash:
     * direção x velocidade base x valor da curva no progresso atual. A
     * velocidade inteira (X, Y e Z) é substituída, por isso o dash vence
     * qualquer outro movimento enquanto dura.
     */
    private void ApplyDashVelocity()
    {
        float progress = Mathf.Clamp01(dashElapsed / dashDuration);
        float multiplier = Mathf.Max(0f, speedCurve.Evaluate(progress));

        rb.linearVelocity = dashDirection * (dashSpeed * multiplier);
    }

    /*
     * GUIA: EndDash
     * Termina o dash: restaura a gravidade original, desmarca o estado e,
     * se foi um fim natural, multiplica a velocidade pela
     * "exitVelocityMultiplier". Depois avisa os outros scripts e dispara o
     * evento onDashEnded.
     */
    private void EndDash(bool applyExitVelocity)
    {
        isDashing = false;

        rb.useGravity = gravityWasEnabled;

        if (applyExitVelocity)
        {
            rb.linearVelocity *= exitVelocityMultiplier;
        }

        SendMessage("OnPlayerDashEnded", SendMessageOptions.DontRequireReceiver);
        onDashEnded?.Invoke();
    }

    /*
     * GUIA: CheckObstacle
     * Se "stopOnObstacle" estiver ligado e estivermos em dash, olha os
     * pontos de contato da colisão. Se alguma superfície estiver virada
     * contra a direção do dash (produto escalar abaixo do limite), encerra
     * o dash. Chão e paredes "de raspão" não cancelam.
     */
    private void CheckObstacle(Collision collision)
    {
        if (!isDashing || !stopOnObstacle) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (Vector3.Dot(normal, dashDirection) < obstacleDotThreshold)
            {
                EndDash(false);
                return;
            }
        }
    }

    // ---------- Direction ----------

    /*
     * GUIA: ResolveDirection
     * Calcula a direção do dash de acordo com o modo escolhido:
     *  - CameraForward: usa a frente da câmera (achatada se
     *    "allowVerticalDash" for falso).
     *  - MovementInput: usa o input (WASD) relativo à "orientation"; sem
     *    input, vai para frente.
     *  - PlayerForward: usa a frente do jogador, no plano horizontal.
     * Se algo der errado (ex.: sem câmera), cai para a frente do jogador.
     */
    private Vector3 ResolveDirection()
    {
        Vector3 direction = Vector3.zero;

        switch (directionMode)
        {
            case DashDirectionMode.CameraForward:
            {
                Transform cam = cameraTransform;
                if (cam == null && Camera.main != null) cam = Camera.main.transform;

                if (cam != null)
                {
                    direction = cam.forward;
                    if (!allowVerticalDash) direction = Flatten(direction);
                }
                break;
            }

            case DashDirectionMode.MovementInput:
            {
                Vector2 input = ReadMoveInput();
                Transform reference = orientation != null ? orientation : transform;

                direction = Flatten(reference.forward) * input.y + Flatten(reference.right) * input.x;
                if (direction.sqrMagnitude < 0.0001f) direction = Flatten(reference.forward);
                break;
            }

            case DashDirectionMode.PlayerForward:
                direction = Flatten(transform.forward);
                break;
        }

        if (direction.sqrMagnitude < 0.000001f) direction = Flatten(transform.forward);
        if (direction.sqrMagnitude < 0.000001f) direction = Vector3.forward;

        return direction.normalized;
    }

    /*
     * GUIA: Flatten
     * Remove a altura (Y) de um vetor e o normaliza. Retorna zero se o vetor
     * ficar vazio.
     */
    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector.sqrMagnitude > 0.000001f ? vector.normalized : Vector3.zero;
    }

    // ---------- Ground ----------

    /*
     * GUIA: IsGrounded
     * Detecção de chão simples, só para recarregar os dashes. Lança uma
     * esfera para baixo a partir do centro do Collider e vê se acerta algo
     * da "groundMask". Esta função é independente: não precisa do
     * PlayerMovement.
     */
    private bool IsGrounded()
    {
        if (col == null) return false;

        Bounds bounds = col.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f;
        float distance = Mathf.Max(0f, bounds.extents.y - radius) + groundCheckDistance;

        return Physics.SphereCast(bounds.center, radius, Vector3.down, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    // ---------- Built-in input readers ----------

    /*
     * GUIA: WasDashPressed
     * Retorna true no frame em que a tecla de dash foi apertada. Funciona no
     * Input System novo e no Input Manager antigo.
     */
    private bool WasDashPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[dashKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(dashKey);
#endif
    }

    /*
     * GUIA: ReadMoveInput
     * Lê o movimento (WASD, setas e analógico esquerdo) para o modo
     * MovementInput. No Input Manager antigo, usa os eixos "Horizontal" e
     * "Vertical". Retorna um vetor com tamanho máximo 1.
     */
    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null) input += gamepad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(input, 1f);
#else
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
    }

    // ---------- Messages from other scripts (optional) ----------
    // This script does not need to receive any message from the other scripts.
}