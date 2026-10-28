/*
 * ============================================================================
 *  PLAYER SLIDE (DESLIZAR)  -  Versão Base
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
 *  Faz o jogador deslizar pelo chão: ao apertar a tecla, ele se agacha
 *  (a cápsula de colisão encolhe) e sai deslizando numa direção, começando
 *  rápido e perdendo velocidade aos poucos, até parar ou soltar a tecla.
 *  Se houver teto baixo, o jogador continua agachado até ter espaço para
 *  levantar. É independente: funciona sozinho, com qualquer script de
 *  movimento baseado em Rigidbody.
 *
 *  RECURSOS PRINCIPAIS
 *  -------------------
 *  - Velocidade inicial, velocidade mínima e taxa de desaceleração.
 *  - Mantém a velocidade que o jogador já tinha, se for maior que a do slide.
 *  - 3 modos de direção: input (WASD), frente do jogador ou velocidade atual.
 *  - Modo "segurar" (solta a tecla = para) ou "até acabar".
 *  - Velocidade mínima para começar (ex.: só deslizar se estiver correndo).
 *  - Cápsula encolhe e só levanta se houver espaço acima (teto).
 *  - Abaixa a câmera suavemente (sem brigar com outros scripts de câmera).
 *  - Cancela se o jogador sair do chão (com tempo de tolerância).
 *  - Eventos (UnityEvent) ao iniciar e ao terminar.
 *  - Funciona com o Input System novo E com o antigo (Input Manager).
 *
 *  COMO USAR (PASSO A PASSO)
 *  -------------------------
 *  1. No objeto do jogador (com Rigidbody e CapsuleCollider), adicione
 *     este script. Mantenha a escala do jogador em (1, 1, 1).
 *  2. (Opcional) Em "Camera Transform" arraste a câmera, para ela abaixar
 *     durante o slide. Deve ser um filho do jogador.
 *  3. Em "Ground Mask" e "Ceiling Mask" escolha as layers de chão e teto
 *     (tire a layer do próprio jogador).
 *  4. Dê Play, ande com WASD e aperte Ctrl para deslizar!
 *
 *  PONTOS IMPORTANTES AO ADAPTAR PARA O SEU PROJETO
 *  ------------------------------------------------
 *  - Feito para Unity 6 ou mais nova (usa "linearVelocity"). Em versões
 *    antigas, troque por "velocity".
 *  - Durante o slide, o seu script de movimento NÃO pode mexer na
 *    velocidade horizontal do Rigidbody. Use "IsSliding" para pausá-lo.
 *    Se usar o PlayerMovement deste pacote, isso já é automático.
 *  - Use "IsCrouched" para saber se a cápsula está encolhida (inclui o caso
 *    de estar preso embaixo de um teto baixo depois do slide).
 *  - Para pular fora do slide, chame CancelSlide() no seu script de pulo
 *    (o PlayerMovement deste pacote já faz isso sozinho).
 *  - Para usar seus próprios controles, desmarque "Use Built In Input" e
 *    chame TrySlide() para começar e CancelSlide() para soltar. Para
 *    escolher a direção por código, use TrySlide(Vector3 direction).
 *  - Para "só deslizar quando estiver correndo", coloque em "Min Speed To
 *    Start" um valor um pouco acima da sua velocidade de andar.
 *  - O slide só mexe na velocidade horizontal. Em rampas, a gravidade do seu
 *    movimento continua agindo na velocidade vertical.
 *
 *  COMUNICAÇÃO COM OUTROS SCRIPTS (OPCIONAL)
 *  -----------------------------------------
 *  Este script envia as mensagens OnPlayerSlideStarted, OnPlayerSlideEnded
 *  e OnPlayerCrouchChanged (SendMessage) para os outros scripts do mesmo
 *  objeto, e recebe OnPlayerDashStarted, OnPlayerDashEnded e
 *  OnPlayerJumped. O PlayerMovement e o PlayerDash entendem essas
 *  mensagens sozinhos. Nenhum deles é obrigatório.
 *
 *  Obs.: o nome da classe precisa ser igual ao nome do arquivo (PlayerSlide.cs).
 * ============================================================================
 */

using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
 * GUIA: SlideDirectionMode
 * Define para onde o slide vai.
 *  - MovementInput: para onde o jogador está tentando andar (WASD).
 *  - PlayerForward: sempre para a frente do jogador.
 *  - CurrentVelocity: na direção em que o jogador já está se movendo.
 * Se não houver direção válida, usa a frente do jogador.
 */
public enum SlideDirectionMode { MovementInput = 0, PlayerForward = 1, CurrentVelocity = 2 }

/*
 * GUIA: PlayerSlide
 * Classe principal do slide. Controla o início, a velocidade durante o
 * deslize, o encolhimento da cápsula, a checagem de teto para levantar, o
 * abaixar da câmera e o fim do slide.
 */
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerSlide : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("If false, this script reads no input. Call TrySlide() / CancelSlide() from your own code.")]
    [SerializeField] private bool useBuiltInInput = true;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key slideKey = Key.LeftCtrl;
#else
    [SerializeField] private KeyCode slideKey = KeyCode.LeftControl;
#endif

    [Header("Direction")]
    [SerializeField] private SlideDirectionMode directionMode = SlideDirectionMode.MovementInput;
    [Tooltip("Reference for 'forward/right'. Empty = the player itself.")]
    [SerializeField] private Transform orientation;

    [Header("Slide")]
    [SerializeField] private float slideStartSpeed = 16f;
    [Tooltip("The slide ends when its speed drops to this value.")]
    [SerializeField] private float slideMinSpeed = 5f;
    [Tooltip("How much speed (units per second) is lost every second while sliding.")]
    [SerializeField] private float slideDecayRate = 6.5f;
    [Tooltip("If true and the player is already faster than 'slideStartSpeed', the slide starts at the player's current speed.")]
    [SerializeField] private bool preserveHigherSpeed = true;
    [Tooltip("If true, releasing the slide key ends the slide. If false, it only ends when the speed reaches the minimum.")]
    [SerializeField] private bool holdToSlide = true;
    [Tooltip("If true, the player must be pressing a movement key to start sliding.")]
    [SerializeField] private bool requireMovementInput = true;
    [Tooltip("Minimum horizontal speed needed to start a slide. Use a value above your walk speed to only slide while running.")]
    [SerializeField] private float minSpeedToStart = 0f;
    [Tooltip("Time (seconds) after a slide ends before another can start.")]
    [SerializeField] private float slideCooldown = 0f;

    [Header("Collider")]
    [Tooltip("Capsule height while crouched / sliding. Must be smaller than the original height.")]
    [SerializeField] private float slideHeight = 1.0f;
    [Tooltip("Layers that block the player from standing up. Remove the player's own layer from here.")]
    [SerializeField] private LayerMask ceilingMask = ~0;

    [Header("Camera (optional)")]
    [Tooltip("Camera (child of the player) that lowers during the slide. Empty = no camera effect.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraLowerAmount = 0.5f;
    [SerializeField] private float cameraLowerSpeed = 15f;

    [Header("Ground")]
    [Tooltip("Layers that count as ground. Remove the player's own layer from here.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [Tooltip("If true, the slide ends when the player leaves the ground.")]
    [SerializeField] private bool cancelWhenAirborne = true;
    [Tooltip("How long (seconds) the player can be airborne before the slide is cancelled.")]
    [SerializeField] private float airborneGraceTime = 0.1f;

    [Header("Events")]
    public UnityEvent onSlideStarted;
    public UnityEvent onSlideEnded;

    // ---------- Internal state ----------

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private float originalHeight;
    private Vector3 originalCenter;

    private bool isSliding;
    private bool isCrouched;
    private float currentSlideSpeed;
    private float startSlideSpeed;
    private Vector3 slideDirection;
    private float cooldownTimer;
    private float airborneTimer;
    private float currentCameraOffset;

    // Reported by PlayerDash through messages
    private bool dashActive;

    // ---------- Public API ----------

    /*
     * GUIA: Propriedades de leitura
     * IsSliding: true durante o deslize (use para pausar seu movimento).
     * IsCrouched: true se a cápsula está encolhida (durante o slide ou
     * esperando espaço para levantar). CurrentSlideSpeed: velocidade atual
     * do slide. SlideProgress01: 0 no início do slide e 1 quando chega na
     * velocidade mínima.
     */
    public bool IsSliding => isSliding;
    public bool IsCrouched => isCrouched;
    public float CurrentSlideSpeed => isSliding ? currentSlideSpeed : 0f;
    public float SlideProgress01 => isSliding ? Mathf.Clamp01(Mathf.InverseLerp(startSlideSpeed, slideMinSpeed, currentSlideSpeed)) : 0f;

    // ---------- Unity messages ----------

    /*
     * GUIA: Awake
     * Pega o Rigidbody e o CapsuleCollider e guarda a altura e o centro
     * originais da cápsula, para poder restaurá-los quando o jogador
     * levantar.
     */
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        originalHeight = capsule.height;
        originalCenter = capsule.center;
    }

    /*
     * GUIA: Update
     * Se "useBuiltInInput" estiver ativo, tenta iniciar o slide quando a
     * tecla for apertada.
     */
    private void Update()
    {
        if (useBuiltInInput && WasSlidePressed())
        {
            TrySlide();
        }
    }

    /*
     * GUIA: FixedUpdate
     * Ciclo da física. Diminui o cooldown. Se estiver deslizando, executa o
     * slide. Se não estiver deslizando mas ainda estiver agachado, tenta
     * levantar (a menos que ainda deva ficar agachado segurando a tecla).
     */
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (cooldownTimer > 0f) cooldownTimer -= dt;

        if (isSliding)
        {
            ExecuteSlide(dt);
        }
        else if (isCrouched && !ShouldStayCrouched())
        {
            TryStandUp();
        }
    }

    /*
     * GUIA: LateUpdate
     * Abaixa e levanta a câmera suavemente. Em vez de forçar uma posição,
     * soma apenas a DIFERENÇA de cada frame, então não briga com outros
     * scripts que também mexem na câmera (bobbing, impacto de pouso etc.).
     */
    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        float target = isCrouched ? cameraLowerAmount : 0f;
        float next = Mathf.Lerp(currentCameraOffset, target, Mathf.Clamp01(cameraLowerSpeed * Time.deltaTime));
        float delta = next - currentCameraOffset;

        if (!Mathf.Approximately(delta, 0f))
        {
            cameraTransform.localPosition -= Vector3.up * delta;
        }

        currentCameraOffset = next;
    }

    /*
     * GUIA: OnDisable
     * Se o script for desligado no meio de um slide ou agachado, restaura
     * a cápsula e a câmera e avisa os outros scripts, para nada ficar preso.
     */
    private void OnDisable()
    {
        if (isSliding)
        {
            isSliding = false;
            SendMessage("OnPlayerSlideEnded", SendMessageOptions.DontRequireReceiver);
            onSlideEnded?.Invoke();
        }

        if (isCrouched) SetCrouched(false);

        if (cameraTransform != null && currentCameraOffset != 0f)
        {
            cameraTransform.localPosition += Vector3.up * currentCameraOffset;
            currentCameraOffset = 0f;
        }
    }

    // ---------- Slide control (public) ----------

    /*
     * GUIA: TrySlide
     * Tenta iniciar um slide usando o modo de direção configurado. Retorna
     * true se começou. Falha se já estiver deslizando, em cooldown, em dash,
     * no ar, devagar demais (minSpeedToStart) ou sem input quando
     * "requireMovementInput" estiver ligado.
     */
    public bool TrySlide()
    {
        if (!CanStartSlide()) return false;

        Vector3 direction = ResolveDirection(out bool hasInput);

        if (requireMovementInput && !hasInput) return false;

        BeginSlide(direction);
        return true;
    }

    /*
     * GUIA: TrySlide (com direção)
     * Igual ao TrySlide, mas você escolhe a direção por código e ignora a
     * exigência de input. A direção é achatada no plano horizontal.
     */
    public bool TrySlide(Vector3 direction)
    {
        if (!CanStartSlide()) return false;

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : GetForward();

        BeginSlide(direction);
        return true;
    }

    /*
     * GUIA: CancelSlide
     * Encerra o slide na hora (se estiver deslizando). A velocidade atual é
     * mantida, então o jogador sai do slide com embalo. O jogador tenta
     * levantar em seguida (se tiver espaço). Use ao pular, tomar dano, etc.
     */
    public void CancelSlide()
    {
        if (isSliding) EndSlide();
    }

    // ---------- Slide internals ----------

    /*
     * GUIA: CanStartSlide
     * Reúne as condições que não dependem da direção: não estar deslizando,
     * cooldown zerado, não estar em dash, ter a velocidade mínima para
     * começar e estar no chão.
     */
    private bool CanStartSlide()
    {
        if (isSliding || cooldownTimer > 0f || dashActive) return false;
        if (GetHorizontalSpeed() < minSpeedToStart) return false;

        return IsGrounded();
    }

    /*
     * GUIA: BeginSlide
     * Inicia o slide: marca o estado, define a velocidade inicial (a maior
     * entre "slideStartSpeed" e a velocidade atual, se
     * "preserveHigherSpeed" estiver ligado), guarda a direção, encolhe a
     * cápsula e avisa os outros scripts e eventos.
     */
    private void BeginSlide(Vector3 direction)
    {
        isSliding = true;
        airborneTimer = 0f;
        slideDirection = direction;

        currentSlideSpeed = preserveHigherSpeed
            ? Mathf.Max(slideStartSpeed, GetHorizontalSpeed())
            : slideStartSpeed;
        startSlideSpeed = currentSlideSpeed;

        SetCrouched(true);

        SendMessage("OnPlayerSlideStarted", SendMessageOptions.DontRequireReceiver);
        onSlideStarted?.Invoke();
    }

    /*
     * GUIA: ExecuteSlide
     * Roda a cada tick da física durante o slide:
     *  1) Define a velocidade horizontal (direção x velocidade do slide),
     *     mantendo a velocidade vertical (gravidade/queda continuam normais).
     *  2) Reduz a velocidade do slide pela taxa de desaceleração.
     *  3) Cancela se o jogador ficar tempo demais no ar.
     *  4) Termina se a velocidade chegou no mínimo ou, no modo "segurar", se a
     *     tecla foi solta.
     */
    private void ExecuteSlide(float dt)
    {
        if (dashActive) return;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(
            slideDirection.x * currentSlideSpeed,
            velocity.y,
            slideDirection.z * currentSlideSpeed
        );

        currentSlideSpeed -= slideDecayRate * dt;

        if (cancelWhenAirborne)
        {
            if (!IsGrounded())
            {
                airborneTimer += dt;

                if (airborneTimer >= airborneGraceTime)
                {
                    EndSlide();
                    return;
                }
            }
            else
            {
                airborneTimer = 0f;
            }
        }

        bool keyReleased = useBuiltInInput && holdToSlide && !IsSlideHeld();

        if (currentSlideSpeed <= slideMinSpeed || keyReleased)
        {
            EndSlide();
        }
    }

    /*
     * GUIA: EndSlide
     * Termina o slide: desmarca o estado, inicia o cooldown, avisa os outros
     * scripts e eventos e tenta levantar (a menos que deva continuar
     * agachado enquanto a tecla estiver segurada).
     */
    private void EndSlide()
    {
        isSliding = false;
        cooldownTimer = slideCooldown;

        SendMessage("OnPlayerSlideEnded", SendMessageOptions.DontRequireReceiver);
        onSlideEnded?.Invoke();

        if (!ShouldStayCrouched())
        {
            TryStandUp();
        }
    }

    /*
     * GUIA: ShouldStayCrouched
     * Retorna true se o jogador deve continuar agachado mesmo sem deslizar:
     * acontece no modo "segurar" enquanto ele ainda aperta a tecla (o slide
     * acabou por falta de velocidade, mas ele segue agachado).
     */
    private bool ShouldStayCrouched()
    {
        return useBuiltInInput && holdToSlide && IsSlideHeld();
    }

    // ---------- Crouch / Stand up ----------

    /*
     * GUIA: SetCrouched
     * Encolhe ou restaura a cápsula de colisão. Ao encolher, o centro desce
     * junto, para o pé do jogador continuar no mesmo lugar. Também avisa os
     * outros scripts (OnPlayerCrouchChanged) quando o estado muda.
     */
    private void SetCrouched(bool crouch)
    {
        if (isCrouched == crouch) return;

        isCrouched = crouch;

        if (crouch)
        {
            capsule.height = slideHeight;
            capsule.center = new Vector3(
                originalCenter.x,
                originalCenter.y - (originalHeight - slideHeight) * 0.5f,
                originalCenter.z
            );
        }
        else
        {
            capsule.height = originalHeight;
            capsule.center = originalCenter;
        }

        SendMessage("OnPlayerCrouchChanged", crouch, SendMessageOptions.DontRequireReceiver);
    }

    /*
     * GUIA: TryStandUp
     * Levanta o jogador, mas só se não houver teto bloqueando. Se houver,
     * continua agachado e tenta de novo a cada tick da física.
     */
    private void TryStandUp()
    {
        if (!isCrouched || isSliding) return;
        if (!CanStandUp()) return;

        SetCrouched(false);
    }

    /*
     * GUIA: CanStandUp
     * Lança uma esfera para cima a partir do topo da cápsula agachada,
     * com o tamanho da diferença de altura. Se acertar algo da
     * "ceilingMask", não há espaço para levantar.
     */
    private bool CanStandUp()
    {
        float extraHeight = originalHeight - slideHeight;
        if (extraHeight <= 0f) return true;

        float radius = capsule.radius * 0.9f;
        Vector3 crouchTop = transform.position + capsule.center + Vector3.up * (capsule.height * 0.5f - radius);

        return !Physics.SphereCast(crouchTop, radius, Vector3.up, out _, extraHeight, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    // ---------- Direction & helpers ----------

    /*
     * GUIA: ResolveDirection
     * Calcula a direção do slide de acordo com o modo escolhido e informa
     * (hasInput) se o jogador está apertando alguma tecla de movimento. Se
     * não houver uma direção válida, usa a frente do jogador.
     */
    private Vector3 ResolveDirection(out bool hasInput)
    {
        Vector2 input = ReadMoveInput();
        hasInput = input.sqrMagnitude > 0.0025f;

        Transform reference = orientation != null ? orientation : transform;
        Vector3 forward = GetForward();

        switch (directionMode)
        {
            case SlideDirectionMode.MovementInput:
            {
                Vector3 direction = forward * input.y + Flatten(reference.right) * input.x;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : forward;
            }

            case SlideDirectionMode.CurrentVelocity:
            {
                Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                return horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : forward;
            }

            default:
                return forward;
        }
    }

    /*
     * GUIA: GetForward
     * Retorna a frente (achatada no plano horizontal) da "orientation" ou do
     * jogador, com segurança contra vetor vazio.
     */
    private Vector3 GetForward()
    {
        Transform reference = orientation != null ? orientation : transform;

        Vector3 forward = Flatten(reference.forward);
        if (forward == Vector3.zero) forward = Flatten(transform.forward);
        if (forward == Vector3.zero) forward = Vector3.forward;

        return forward;
    }

    /*
     * GUIA: GetHorizontalSpeed
     * Retorna a velocidade horizontal atual do Rigidbody (ignora a queda).
     */
    private float GetHorizontalSpeed()
    {
        return new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
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

    /*
     * GUIA: IsGrounded
     * Detecção de chão simples. Lança uma esfera para baixo a partir do
     * centro da cápsula e vê se acerta algo da "groundMask". Independente do
     * PlayerMovement.
     */
    private bool IsGrounded()
    {
        Bounds bounds = capsule.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f;
        float distance = Mathf.Max(0f, bounds.extents.y - radius) + groundCheckDistance;

        return Physics.SphereCast(bounds.center, radius, Vector3.down, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    // ---------- Messages from other scripts (optional) ----------

    /*
     * GUIA: Mensagens recebidas (OnPlayerDashStarted, OnPlayerDashEnded,
     * OnPlayerJumped)
     * Chamadas automaticamente pelos scripts PlayerDash e PlayerMovement
     * (se existirem no mesmo objeto). Ao começar um dash, o slide é cancelado
     * e não pode começar outro até o dash acabar. Ao pular, o slide é
     * cancelado (mantendo a velocidade). Os nomes precisam ser exatamente
     * estes.
     */
    private void OnPlayerDashStarted()
    {
        dashActive = true;
        if (isSliding) EndSlide();
    }

    private void OnPlayerDashEnded() { dashActive = false; }

    private void OnPlayerJumped()
    {
        if (isSliding) EndSlide();
    }

    // ---------- Built-in input readers ----------

    /*
     * GUIA: WasSlidePressed / IsSlideHeld
     * Leem a tecla de slide. "Pressed" = apertou neste frame; "Held" =
     * está segurando. Funcionam no Input System novo e no antigo.
     */
    private bool WasSlidePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[slideKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(slideKey);
#endif
    }

    private bool IsSlideHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[slideKey].isPressed;
#else
        return Input.GetKey(slideKey);
#endif
    }

    /*
     * GUIA: ReadMoveInput
     * Lê o movimento (WASD, setas e analógico esquerdo) para saber a direção
     * do slide e se o jogador está se movendo. No Input Manager antigo, usa
     * os eixos "Horizontal" e "Vertical". Retorna um vetor com tamanho
     * máximo 1.
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
}