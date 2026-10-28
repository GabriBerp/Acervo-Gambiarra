/*
 * ============================================================================
 *  PLAYER MOVEMENT (MOVIMENTAÇÃO BÁSICA)  -  Versão Base
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
 *  Movimentação básica de jogador baseada em Rigidbody: andar, correr e
 *  pular. É pensada para ser simples de configurar e de alterar, e funciona
 *  tanto em primeira quanto em terceira pessoa.
 *
 *  RECURSOS PRINCIPAIS
 *  -------------------
 *  - Andar e correr (segurar, alternar com uma tecla, ou sempre correndo).
 *  - Aceleração e desaceleração separadas para chão e ar (valores altos
 *    deixam o movimento "seco"; valores baixos deixam mais "escorregadio").
 *  - Pulo com altura definida em unidades, pulo de altura variável (soltar
 *    o botão corta o pulo), pulo duplo/múltiplo opcional.
 *  - Coyote time (pular logo depois de sair da beirada) e jump buffer
 *    (apertar pulo um pouco antes de pousar).
 *  - Gravidade customizada (mais "pesada" na queda) com velocidade
 *    terminal.
 *  - Detecção de chão com SphereCast e ângulo máximo de inclinação.
 *  - Eventos (UnityEvent) ao pular e ao pousar.
 *  - Funciona com o Input System novo E com o antigo (Input Manager).
 *
 *  COMO USAR (PASSO A PASSO)
 *  -------------------------
 *  1. Crie um objeto para o jogador com um CapsuleCollider (a altura
 *     padrão é 2 e o raio 0.5). Mantenha a escala em (1, 1, 1).
 *  2. Adicione este script (ele adiciona o Rigidbody automaticamente).
 *  3. Em "Ground Mask" escolha as layers que contam como chão. IMPORTANTE:
 *     tire a layer do próprio jogador dessa máscara.
 *  4. (Opcional) Em "Movement Reference" arraste um Transform para definir
 *     "para onde é frente". Em primeira pessoa deixe vazio (usa o próprio
 *     jogador). Em terceira pessoa arraste a câmera, e o jogador andará
 *     relativo a ela.
 *  5. Dê Play! Padrão: WASD move, Shift corre, Espaço pula.
 *
 *  PONTOS IMPORTANTES AO ADAPTAR PARA O SEU PROJETO
 *  ------------------------------------------------
 *  - Este script NÃO cuida da câmera nem do giro do mouse. Quem girar o
 *    jogador (ou a câmera) deve ser o seu script de câmera.
 *  - Feito para Unity 6 ou mais nova (usa "linearVelocity" e
 *    "PhysicsMaterial"). Em versões antigas, troque por "velocity" e
 *    "PhysicMaterial".
 *  - A gravidade da Unity (useGravity) é DESLIGADA no Awake, pois este
 *    script aplica a própria gravidade. Isso é proposital.
 *  - Para usar seus próprios controles (celular, gamepad, rebind), desmarque
 *    "Use Built In Input" e chame as funções públicas: SetMoveInput(),
 *    RequestJump(), SetJumpHeld(), SetRunHeld() e ToggleRun().
 *  - Para gamepad com o Input System novo, o analógico esquerdo já é lido
 *    para o movimento. As teclas de pular/correr são de teclado.
 *  - Para um pulo mais "flutuante", diminua "Gravity Multiplier". Para um
 *    movimento mais responsivo, aumente as acelerações.
 *
 *  INTEGRAÇÃO AUTOMÁTICA COM PlayerDash E PlayerSlide (OPCIONAL)
 *  -------------------------------------------------------------
 *  Se no mesmo objeto existirem os scripts PlayerDash e/ou PlayerSlide, eles
 *  conversam entre si por mensagens (SendMessage), SEM dependência de
 *  código. Este script, por exemplo, para de mexer na velocidade durante o
 *  dash e o slide automaticamente. Se você não usar esses scripts, nada
 *  muda. Mensagens que este script recebe: OnPlayerDashStarted,
 *  OnPlayerDashEnded, OnPlayerSlideStarted, OnPlayerSlideEnded e
 *  OnPlayerCrouchChanged. Mensagem que ele envia: OnPlayerJumped.
 *
 *  Obs.: o nome da classe precisa ser igual ao nome do arquivo (PlayerMovement.cs).
 * ============================================================================
 */

using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
 * GUIA: RunMode
 * Define como o jogador corre.
 *  - Hold: corre enquanto segura a tecla de correr.
 *  - Toggle: apertar a tecla alterna entre andar e correr.
 *  - AlwaysRun: sempre corre, a tecla é ignorada.
 */
public enum RunMode { Hold = 0, Toggle = 1, AlwaysRun = 2 }

/*
 * GUIA: PlayerMovement
 * Classe principal. Recebe o input (do teclado/gamepad ou de fora), calcula
 * a velocidade horizontal desejada, aplica pulo e gravidade customizada e
 * detecta o chão. Tudo é feito pela física (Rigidbody) no FixedUpdate.
 */
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Defines which way is 'forward'. Empty = the player itself (first person). Use the camera for third person.")]
    [SerializeField] private Transform movementReference;

    [Header("Input")]
    [Tooltip("If false, this script reads no input by itself. Call SetMoveInput(), RequestJump(), etc. from your own code.")]
    [SerializeField] private bool useBuiltInInput = true;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key jumpKey = Key.Space;
    [SerializeField] private Key runKey = Key.LeftShift;
#else
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
#endif
    [SerializeField] private RunMode runMode = RunMode.Hold;
    [Tooltip("Only used in Toggle mode: if true, the player starts already running.")]
    [SerializeField] private bool startRunning = false;

    [Header("Speed")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 10f;
    [Tooltip("Speed multiplier while crouched (only used when PlayerSlide is on the same object).")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;

    [Header("Acceleration (units per second squared)")]
    [Tooltip("How fast the player reaches the target speed on the ground. Very high = instant.")]
    [SerializeField] private float groundAcceleration = 80f;
    [Tooltip("How fast the player stops on the ground when there is no input.")]
    [SerializeField] private float groundDeceleration = 60f;
    [Tooltip("How fast the player changes horizontal speed in the air when there is input.")]
    [SerializeField] private float airAcceleration = 30f;
    [Tooltip("How fast the player slows down in the air with no input. 0 = keeps flying at the same speed.")]
    [SerializeField] private float airDeceleration = 0f;
    [Tooltip("How fast speed above the target speed (e.g. after a slide) is lost while there is input.")]
    [SerializeField] private float overspeedDeceleration = 20f;

    [Header("Jump")]
    [Tooltip("Jump height in world units (when the jump button is held until the peak).")]
    [SerializeField] private float jumpHeight = 2.5f;
    [Tooltip("Extra jumps allowed in the air. 0 = no double jump, 1 = double jump...")]
    [SerializeField] private int maxAirJumps = 0;
    [Tooltip("How long (seconds) a jump press is remembered before landing.")]
    [SerializeField] private float jumpBufferTime = 0.15f;
    [Tooltip("How long (seconds) after leaving the ground the player can still jump.")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Gravity")]
    [Tooltip("Base gravity multiplier. Bigger = heavier character and faster jump arc.")]
    [SerializeField] private float gravityMultiplier = 2.5f;
    [Tooltip("Extra multiplier while falling.")]
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [Tooltip("Extra multiplier while rising with the jump button released (variable jump height).")]
    [SerializeField] private float lowJumpGravityMultiplier = 2f;
    [Tooltip("Terminal fall speed.")]
    [SerializeField] private float maxFallSpeed = 30f;

    [Header("Ground Detection")]
    [Tooltip("Layers that count as ground. Remove the player's own layer from here.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float groundCheckRadiusMultiplier = 0.9f;
    [SerializeField] private float groundCheckSkin = 0.05f;
    [Tooltip("Slopes steeper than this angle are not considered ground.")]
    [SerializeField] private float maxGroundAngle = 60f;

    [Header("Physics Setup")]
    [Tooltip("If true, configures the Rigidbody and collider automatically (zero friction, interpolation, continuous collisions, no tipping over).")]
    [SerializeField] private bool autoConfigurePhysics = true;

    [Header("Events")]
    public UnityEvent onJumped;
    [Tooltip("Receives the impact speed (how fast the player was falling) as a parameter.")]
    public UnityEvent<float> onLanded;

    // ---------- Internal state ----------

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool runHeld;
    private bool runToggled;

    private float jumpBufferCounter;
    private float coyoteCounter;
    private int airJumpsLeft;
    private float groundIgnoreTimer;

    private bool isGrounded;
    private bool wasGrounded;
    private float lastVerticalVelocity;

    private bool movementEnabled = true;
    private float lockTimer;

    // States reported by PlayerDash / PlayerSlide through messages
    private bool dashActive;
    private bool slideActive;
    private bool crouched;

    // ---------- Public API ----------

    /*
     * GUIA: Propriedades de leitura
     * IsGrounded: true se o jogador está no chão.
     * IsRunning: true se está correndo de fato (quer correr, está se movendo
     * e não está agachado). MoveInput: input atual (x = lateral, y = frente).
     * HorizontalSpeed: velocidade horizontal real. VerticalSpeed: velocidade
     * vertical real. Úteis para animações, sons, FOV e interface.
     */
    public bool IsGrounded => isGrounded;
    public bool IsRunning => WantsToRun && moveInput.sqrMagnitude > 0.0025f && !crouched;
    public Vector2 MoveInput => moveInput;
    public float HorizontalSpeed => rb != null ? new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude : 0f;
    public float VerticalSpeed => rb != null ? rb.linearVelocity.y : 0f;

    /*
     * GUIA: WantsToRun
     * Diz se o jogador "quer" correr, de acordo com o modo escolhido
     * (segurar, alternar ou sempre correr). É usada por IsRunning e pelo
     * cálculo da velocidade alvo.
     */
    private bool WantsToRun
    {
        get
        {
            switch (runMode)
            {
                case RunMode.Hold: return runHeld;
                case RunMode.Toggle: return runToggled;
                default: return true;
            }
        }
    }

    /*
     * GUIA: IsHorizontalControlBlocked
     * Retorna true quando este script NÃO deve controlar a velocidade
     * horizontal: movimento desativado por fora, bloqueio temporário
     * (LockMovement) ou slide em andamento (o PlayerSlide assume o controle).
     */
    private bool IsHorizontalControlBlocked => !movementEnabled || lockTimer > 0f || slideActive;

    // ---------- Unity messages ----------

    /*
     * GUIA: Awake
     * Pega as referências do Rigidbody e do CapsuleCollider, DESLIGA a
     * gravidade nativa da Unity (a gravidade é aplicada manualmente por este
     * script), define se o jogador começa correndo e, se ativado,
     * configura a física automaticamente.
     */
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        runToggled = startRunning;

        if (autoConfigurePhysics)
        {
            ConfigurePhysics();
        }
    }

    /*
     * GUIA: Update
     * Roda todo frame. Se "useBuiltInInput" estiver ativo, lê o teclado/
     * gamepad e repassa os valores para as funções públicas de input.
     * Ler o input aqui (e não no FixedUpdate) evita perder apertos de tecla.
     */
    private void Update()
    {
        if (!useBuiltInInput) return;

        SetMoveInput(ReadMoveInput());

        if (WasJumpPressed()) RequestJump();
        SetJumpHeld(IsJumpHeld());

        if (runMode == RunMode.Hold)
        {
            SetRunHeld(IsRunHeld());
        }
        else if (runMode == RunMode.Toggle && WasRunPressed())
        {
            ToggleRun();
        }
    }

    /*
     * GUIA: FixedUpdate
     * Ciclo da física. Em ordem: atualiza o bloqueio temporário, detecta o
     * chão, trata o pulo, aplica o movimento horizontal e a gravidade. Durante
     * um dash, tudo isso é pulado (o PlayerDash controla a velocidade).
     */
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (lockTimer > 0f) lockTimer -= dt;

        UpdateGroundState(dt);

        if (!dashActive)
        {
            HandleJump();

            if (!IsHorizontalControlBlocked)
            {
                ApplyHorizontalMovement(dt);
            }

            ApplyGravity(dt);
        }

        if (jumpBufferCounter > 0f) jumpBufferCounter -= dt;

        lastVerticalVelocity = rb.linearVelocity.y;
    }

    // ---------- Input (public) ----------

    /*
     * GUIA: SetMoveInput
     * Define o input de movimento: x = esquerda/direita (-1 a 1) e
     * y = trás/frente (-1 a 1). Use isto com seu próprio sistema de input.
     */
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    /*
     * GUIA: RequestJump
     * Pede um pulo. O pedido fica guardado por "jumpBufferTime" segundos,
     * então se o jogador apertar um pouco antes de pousar, o pulo ainda sai.
     * Chame uma vez quando o botão for APERTADO (não todo frame).
     */
    public void RequestJump()
    {
        jumpBufferCounter = Mathf.Max(jumpBufferTime, Time.fixedDeltaTime);
    }

    /*
     * GUIA: SetJumpHeld
     * Informa se o botão de pulo está sendo segurado. Soltar o botão durante
     * a subida corta o pulo (pulo de altura variável). Chame todo frame.
     */
    public void SetJumpHeld(bool held)
    {
        jumpHeld = held;
    }

    /*
     * GUIA: SetRunHeld
     * Informa se o botão de correr está segurado (usado no modo Hold).
     */
    public void SetRunHeld(bool held)
    {
        runHeld = held;
    }

    /*
     * GUIA: ToggleRun
     * Alterna entre andar e correr (usado no modo Toggle).
     */
    public void ToggleRun()
    {
        runToggled = !runToggled;
    }

    // ---------- Control (public) ----------

    /*
     * GUIA: LockMovement
     * Bloqueia o controle horizontal por X segundos, sem apagar a
     * velocidade. Ótimo para empurrões, recuos, knockback e ganchos: o
     * impulso aplicado não é sobrescrito no próximo tick da física.
     */
    public void LockMovement(float duration)
    {
        lockTimer = Mathf.Max(lockTimer, duration);
    }

    /*
     * GUIA: SetMovementEnabled
     * Liga ou desliga o controle horizontal por tempo indeterminado
     * (cutscenes, menus, morte). A gravidade e o pulo continuam funcionando.
     */
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
    }

    // ---------- Movement ----------

    /*
     * GUIA: ApplyHorizontalMovement
     * Calcula a velocidade horizontal desejada (direção do input x velocidade
     * de andar/correr) e aproxima a velocidade atual dela usando a
     * aceleração correta: chão ou ar, com input ou sem input. Se a
     * velocidade atual estiver ACIMA da desejada (ex.: saiu de um slide),
     * ela diminui pela "overspeedDeceleration", mantendo o embalo por um
     * instante. A velocidade vertical nunca é alterada aqui.
     */
    private void ApplyHorizontalMovement(float dt)
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 currentHorizontal = new Vector3(velocity.x, 0f, velocity.z);

        bool hasInput = moveInput.sqrMagnitude > 0.0025f;
        Vector3 direction = GetInputDirection();

        float targetSpeed = WantsToRun ? runSpeed : walkSpeed;
        if (crouched) targetSpeed = walkSpeed * crouchSpeedMultiplier;

        Vector3 targetVelocity = hasInput ? direction * targetSpeed : Vector3.zero;

        float rate;
        if (hasInput)
        {
            bool overspeed = currentHorizontal.magnitude > targetSpeed + 0.1f;
            rate = overspeed ? overspeedDeceleration : (isGrounded ? groundAcceleration : airAcceleration);
        }
        else
        {
            rate = isGrounded ? groundDeceleration : airDeceleration;
        }

        Vector3 next = Vector3.MoveTowards(currentHorizontal, targetVelocity, rate * dt);

        rb.linearVelocity = new Vector3(next.x, velocity.y, next.z);
    }

    /*
     * GUIA: GetInputDirection
     * Converte o input (x, y) em uma direção no mundo, relativa ao
     * "movementReference" (ou ao próprio jogador). A direção é achatada no
     * plano horizontal, então olhar para cima/baixo não muda a velocidade.
     */
    private Vector3 GetInputDirection()
    {
        Transform reference = movementReference != null ? movementReference : transform;

        Vector3 forward = Flatten(reference.forward);
        if (forward == Vector3.zero) forward = Flatten(reference.up);
        Vector3 right = Flatten(reference.right);

        Vector3 direction = forward * moveInput.y + right * moveInput.x;

        if (direction.sqrMagnitude > 1f) direction.Normalize();

        return direction;
    }

    // ---------- Jump & Gravity ----------

    /*
     * GUIA: HandleJump
     * Decide se o pulo guardado (buffer) deve acontecer agora. Se o jogador
     * está no chão (ou ainda dentro do coyote time), faz o pulo normal. Se
     * não, e ainda houver pulos aéreos sobrando, faz um pulo no ar.
     */
    private void HandleJump()
    {
        if (jumpBufferCounter <= 0f) return;

        if (isGrounded || coyoteCounter > 0f)
        {
            PerformJump();
        }
        else if (airJumpsLeft > 0)
        {
            airJumpsLeft--;
            PerformJump();
        }
    }

    /*
     * GUIA: PerformJump
     * Executa o pulo. Calcula a velocidade vertical necessária para atingir
     * exatamente "jumpHeight" com a gravidade configurada (v = raiz de 2 x g
     * x altura), consome o buffer e o coyote time, ignora o chão por um
     * instante (para não contar o mesmo chão duas vezes) e avisa os outros
     * scripts e eventos.
     */
    private void PerformJump()
    {
        float gravity = Mathf.Abs(Physics.gravity.y) * gravityMultiplier;
        float jumpVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, jumpHeight));

        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpVelocity;
        rb.linearVelocity = velocity;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        groundIgnoreTimer = 0.1f;
        isGrounded = false;

        SendMessage("OnPlayerJumped", SendMessageOptions.DontRequireReceiver);
        onJumped?.Invoke();
    }

    /*
     * GUIA: ApplyGravity
     * Aplica a gravidade manual. Base = gravidade da Unity x
     * gravityMultiplier. Caindo, soma o fallGravityMultiplier (queda mais
     * pesada). Subindo com o botão de pulo solto, soma o
     * lowJumpGravityMultiplier (pulo curto). A queda é limitada por
     * maxFallSpeed.
     */
    private void ApplyGravity(float dt)
    {
        Vector3 velocity = rb.linearVelocity;

        float multiplier = gravityMultiplier;

        if (velocity.y < 0f)
        {
            multiplier *= fallGravityMultiplier;
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            multiplier *= lowJumpGravityMultiplier;
        }

        velocity.y += Physics.gravity.y * multiplier * dt;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

        rb.linearVelocity = velocity;
    }

    // ---------- Ground ----------

    /*
     * GUIA: UpdateGroundState
     * Atualiza se o jogador está no chão. Enquanto no chão, renova o coyote
     * time e os pulos aéreos; no ar, o coyote time diminui. Também detecta o
     * momento exato do pouso e dispara o evento onLanded com a velocidade de
     * impacto.
     */
    private void UpdateGroundState(float dt)
    {
        bool grounded = CheckGround();

        // Logo após um pulo o chão é ignorado por um instante, para evitar
        // que o mesmo contato com o chão renove o pulo.
        if (groundIgnoreTimer > 0f)
        {
            groundIgnoreTimer -= dt;
            grounded = false;
        }

        isGrounded = grounded;

        if (grounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsLeft = maxAirJumps;
        }
        else
        {
            coyoteCounter -= dt;
        }

        if (grounded && !wasGrounded)
        {
            onLanded?.Invoke(Mathf.Abs(lastVerticalVelocity));
        }

        wasGrounded = grounded;
    }

    /*
     * GUIA: CheckGround
     * Lança uma esfera para baixo a partir do pé da cápsula. Se acertar algo
     * da "groundMask" com inclinação menor que "maxGroundAngle", conta
     * como chão. A "skin" dá uma folga para evitar falhas quando o jogador
     * apenas encosta no chão.
     */
    private bool CheckGround()
    {
        float radius = capsule.radius * groundCheckRadiusMultiplier;

        Vector3 bottom = transform.position + capsule.center - Vector3.up * (capsule.height * 0.5f);
        Vector3 castStart = bottom + Vector3.up * (radius + groundCheckSkin);
        float castDistance = groundCheckSkin + groundCheckDistance;

        if (Physics.SphereCast(castStart, radius, Vector3.down, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle;
        }

        return false;
    }

    // ---------- Setup ----------

    /*
     * GUIA: ConfigurePhysics
     * Aplica as configurações recomendadas: Rigidbody não-kinematic com
     * interpolação (movimento suave) e colisão contínua (não atravessa
     * paredes), travamento de rotação em X e Z (o jogador não tomba) e um
     * material sem atrito (não "gruda" nas paredes ao pular encostado).
     */
    private void ConfigurePhysics()
    {
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        PhysicsMaterial noFriction = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        capsule.material = noFriction;
    }

    /*
     * GUIA: Flatten
     * Remove a altura (Y) de um vetor e o normaliza. Retorna zero se o vetor
     * ficar vazio (ex.: olhando exatamente para cima ou para baixo).
     */
    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector.sqrMagnitude > 0.000001f ? vector.normalized : Vector3.zero;
    }

    // ---------- Messages from PlayerDash / PlayerSlide (optional) ----------

    /*
     * GUIA: Mensagens recebidas (OnPlayerDashStarted, OnPlayerDashEnded,
     * OnPlayerSlideStarted, OnPlayerSlideEnded, OnPlayerCrouchChanged)
     * Chamadas automaticamente pelos scripts PlayerDash e PlayerSlide (se
     * existirem no mesmo objeto). Servem para este script saber quando deve
     * "sair da frente": durante o dash ele não mexe em nada; durante o slide
     * ele não controla a velocidade horizontal (mas o pulo funciona); e,
     * agachado, a velocidade é multiplicada por crouchSpeedMultiplier.
     * Os nomes precisam ser exatamente estes.
     */
    private void OnPlayerDashStarted() { dashActive = true; }
    private void OnPlayerDashEnded() { dashActive = false; }
    private void OnPlayerSlideStarted() { slideActive = true; }
    private void OnPlayerSlideEnded() { slideActive = false; }
    private void OnPlayerCrouchChanged(bool isCrouched) { crouched = isCrouched; }

    // ---------- Built-in input readers ----------

    /*
     * GUIA: ReadMoveInput
     * Lê o movimento do teclado (WASD e setas) e do analógico esquerdo do
     * gamepad no Input System novo. No Input Manager antigo, lê os eixos
     * "Horizontal" e "Vertical". Retorna um vetor com tamanho máximo 1.
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

    /*
     * GUIA: WasJumpPressed, IsJumpHeld, WasRunPressed, IsRunHeld
     * Leem as teclas de pular e correr. "Pressed" = apertou neste frame;
     * "Held" = está segurando. Funcionam no Input System novo e no antigo.
     */
    private bool WasJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[jumpKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(jumpKey);
#endif
    }

    private bool IsJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[jumpKey].isPressed;
#else
        return Input.GetKey(jumpKey);
#endif
    }

    private bool WasRunPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[runKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(runKey);
#endif
    }

    private bool IsRunHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[runKey].isPressed;
#else
        return Input.GetKey(runKey);
#endif
    }
}