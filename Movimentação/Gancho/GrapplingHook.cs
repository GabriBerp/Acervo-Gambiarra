/*
 * ============================================================================
 *  GRAPPLING HOOK (GANCHO COM BALANÇO)  -  Versão Base
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
 *  Dispara um gancho (corda) na direção em que a mão/arma está apontando.
 *  Se a corda acertar uma superfície válida (marcada com uma das tags da
 *  lista "grappleTags"), ela gruda e PUXA o jogador em direção ao ponto
 *  atingido.
 *
 *  O PULO DO GATO: a corda não é um trilho reto. Ela funciona como uma
 *  RESTRIÇÃO DE DISTÂNCIA. O jogador pode se mover para os lados dentro do
 *  raio da corda, e a velocidade lateral (momentum) dele nunca é apagada.
 *  Por isso dá pra fazer curvas e balançar (swing), em vez de ser arrastado
 *  em linha reta. O jogador também pode apertar WASD para empurrar levemente
 *  a curva para o lado que quiser, e continua olhando livremente com a câmera
 *  enquanto é puxado (este script não mexe na câmera).
 *
 *  RECURSOS PRINCIPAIS
 *  -------------------
 *  - Disparo com voo visual da corda (LineRenderer).
 *  - Puxão + recolhimento gradual da corda (a corda vai encurtando).
 *  - Controle leve do jogador durante o balanço.
 *  - Segue alvos que se movem (plataformas móveis, por exemplo).
 *  - Sistema de cargas opcional (gasta ao atirar, recarrega tudo de uma vez).
 *  - Recuo (repel) opcional ao bater no alvo.
 *  - Eventos (UnityEvent) ao grudar e ao soltar, para você ligar sons,
 *    efeitos, animações etc.
 *
 *  COMO USAR (PASSO A PASSO)
 *  -------------------------
 *  1. Adicione este script em um objeto da mão/arma do jogador (ou na
 *     câmera). Ele precisa de um Rigidbody de jogador para puxar.
 *  2. Crie um objeto filho vazio onde a corda sai ("ropeOrigin"). A corda
 *     é disparada para onde o eixo AZUL (forward / Z) desse objeto aponta.
 *     Normalmente ele fica na câmera ou na mão, olhando para frente.
 *  3. Adicione um LineRenderer (largura pequena, material simples) e
 *     arraste no campo "ropeLine".
 *  4. (Opcional) Arraste um MeshRenderer da "ponta" do gancho em "hookMesh".
 *     Ele só aparece enquanto a corda está ativa.
 *  5. Arraste o Rigidbody do jogador em "playerRigidbody".
 *  6. Coloque as tags das superfícies agarráveis na lista "grappleTags"
 *     (padrão: "Ground") e aplique essas tags nos objetos da cena.
 *  7. Dê Play! Botão direito do mouse = atirar. Soltar o botão = largar.
 *
 *  PONTOS IMPORTANTES AO ADAPTAR PARA O SEU PROJETO
 *  ------------------------------------------------
 *  - IMPORTANTE: o seu script de movimento do jogador deve PARAR de mexer
 *    na velocidade do Rigidbody enquanto "IsGrappling" for true. Se ele
 *    continuar sobrescrevendo a velocidade, vai brigar com a corda.
 *    Exemplo no seu movimento:  if (grapplingHook.IsGrappling) return;
 *  - O jogador precisa ter um Rigidbody NÃO-kinematic. Se usar
 *    CharacterController, será preciso adaptar a física.
 *  - Este código usa "linearVelocity" (Unity 6). Em versões antigas da
 *    Unity, troque por "velocity".
 *  - O input usa o sistema antigo (Input.GetMouseButton, GetAxis). Se o seu
 *    projeto usa o novo Input System, troque essas linhas.
 *  - Ajuste "pullAcceleration", "reelInSpeed" e "playerSwingControlForce"
 *    para mudar a sensação: mais puxão = mais frenético; mais controle =
 *    curvas mais fáceis.
 *  - Depois do recuo (repel), seu script de movimento pode sobrescrever a
 *    velocidade. Se isso acontecer, bloqueie o movimento por um instante
 *    usando o evento "onGrappleReleased".
 *  - O nome da classe precisa ser igual ao nome do arquivo (GrapplingHook.cs).
 * ============================================================================
 */

using UnityEngine;
using UnityEngine.Events;

/*
 * GUIA: GrapplingHook
 * Classe principal do gancho. Cuida do disparo, do voo visual da corda,
 * do puxão/balanço baseado em física, das cargas, do recuo e do desenho
 * da corda na tela.
 */
public class GrapplingHook : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Rigidbody of the player. It is the object that gets pulled by the rope.")]
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Input")]
    [Tooltip("Mouse button that shoots the hook (0 = left, 1 = right, 2 = middle).")]
    [SerializeField] private int shootMouseButton = 1;

    [Header("Grappable Surfaces")]
    [Tooltip("Tags of the objects the hook can stick to.")]
    [SerializeField] private string[] grappleTags = { "Ground" };

    [Header("Rope Settings")]
    [Tooltip("LineRenderer used to draw the rope.")]
    [SerializeField] private LineRenderer ropeLine;
    [Tooltip("Where the rope comes out of. The rope is shot toward this object's forward (blue axis).")]
    [SerializeField] private Transform ropeOrigin;
    [Tooltip("Optional mesh for the hook tip. Only visible while the rope is active.")]
    [SerializeField] private MeshRenderer hookMesh;
    [SerializeField] private float ropeSpeed = 60f;
    [SerializeField] private float ropeMaxDistance = 30f;

    [Header("Swing Settings")]
    [Tooltip("Pulling force toward the hook point. High values = faster and more intense pull.")]
    [SerializeField] private float pullAcceleration = 65f;
    [Tooltip("How much the rope shrinks per second (reeling in).")]
    [SerializeField] private float reelInSpeed = 6f;
    [Tooltip("Minimum length the rope can shrink to.")]
    [SerializeField] private float minRopeLength = 1.5f;
    [Tooltip("Distance to the target at which the hook releases by itself.")]
    [SerializeField] private float autoReleaseDistance = 2f;
    [Tooltip("Tolerance radius to consider that the player really touched the target.")]
    [SerializeField] private float collisionCheckRadius = 0.6f;
    [Tooltip("Safety: maximum time (seconds) a hook can stay active.")]
    [SerializeField] private float maxGrappleDuration = 6f;

    [Header("Player Control During Swing")]
    [Tooltip("Small force applied in the direction of the player's input (WASD) while grappling, so the curve can be nudged.")]
    [SerializeField] private float playerSwingControlForce = 8f;

    [Header("Charges (optional)")]
    [Tooltip("If false, the hook can be shot infinitely.")]
    [SerializeField] private bool useCharges = true;
    [SerializeField] private int maxCharges = 3;
    [Tooltip("Seconds to refill ALL charges once they reach zero.")]
    [SerializeField] private float rechargeCooldown = 5f;

    [Header("Repel / Recoil (optional)")]
    [Tooltip("If true, the player bounces back when really touching the target.")]
    [SerializeField] private bool enableRepel = true;
    [SerializeField] private float repelForce = 12f;
    [SerializeField] private float repelUpwardBoost = 5f;
    [SerializeField] private float repelMinUpward = 1.5f;
    [Tooltip("If the travel direction points up more than this value, the bigger upward boost is used.")]
    [SerializeField] private float upwardTargetThreshold = 0.15f;

    [Header("Events")]
    public UnityEvent onGrappleStarted;
    public UnityEvent onGrappleReleased;

    // ---------- Internal state ----------

    private bool ropeFlying;
    private bool isGrappling;
    private Vector3 ropeEnd;
    private Vector3 grapplePoint;

    private Vector3 shootOrigin;
    private Vector3 shootDirection;

    private bool pendingHit;
    private Vector3 pendingHitPoint;
    private float pendingHitDistance;

    private Transform hitTransform;
    private Collider hitCollider;
    private Vector3 localHitOffset;

    private float ropeLength;
    private float grappleTimer;

    private int currentCharges;
    private bool isRecharging;
    private float rechargeTimer;

    // ---------- Public API ----------

    /*
     * GUIA: IsGrappling
     * Fica true enquanto o gancho está grudado e puxando o jogador.
     * Use no seu script de movimento para desligar o controle normal
     * (ou reduzi-lo) durante o puxão.
     */
    public bool IsGrappling => isGrappling;

    /*
     * GUIA: CurrentCharges, MaxCharges, IsRecharging, RechargeProgress01
     * Informações das cargas para usar na interface (HUD). RechargeProgress01
     * vai de 0 a 1 durante a recarga e vale 1 quando não está recarregando.
     */
    public int CurrentCharges => currentCharges;
    public int MaxCharges => maxCharges;
    public bool IsRecharging => isRecharging;
    public float RechargeProgress01 => isRecharging ? 1f - Mathf.Clamp01(rechargeTimer / rechargeCooldown) : 1f;

    /*
     * GUIA: DistanceToGrappleTarget
     * Distância atual entre o jogador e o ponto onde o gancho está preso.
     * Retorna infinito se não estiver grudado em nada.
     */
    public float DistanceToGrappleTarget =>
        (isGrappling && playerRigidbody != null)
            ? Vector3.Distance(playerRigidbody.position, grapplePoint)
            : float.PositiveInfinity;

    /*
     * GUIA: Start
     * Executa uma vez ao iniciar. Esconde a ponta do gancho, prepara o
     * LineRenderer com 2 pontos (origem e ponta), tenta achar o Rigidbody
     * do jogador caso não tenha sido colocado no Inspector e enche as cargas.
     */
    private void Start()
    {
        if (hookMesh != null) hookMesh.enabled = false;

        ropeLine.positionCount = 2;
        ropeLine.enabled = false;
        ropeLine.numCornerVertices = 5;
        ropeLine.numCapVertices = 5;

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponentInParent<Rigidbody>();
        }

        currentCharges = maxCharges;
    }

    /*
     * GUIA: Update
     * Roda todo frame. Trata a recarga, lê o input (atirar ao apertar o
     * botão, soltar ao largar), anima o voo da corda, mantém o ponto de
     * gancho colado no alvo e redesenha a corda.
     */
    private void Update()
    {
        HandleChargeRecharge();

        if (Input.GetMouseButtonDown(shootMouseButton))
        {
            TryShootRope();
        }

        if (Input.GetMouseButtonUp(shootMouseButton))
        {
            Release();
        }

        if (ropeFlying)
        {
            UpdateRopeFlight();
        }

        if (isGrappling)
        {
            UpdateGrapplePointFromTarget();
        }

        UpdateRopeVisual();
    }

    /*
     * GUIA: FixedUpdate
     * Roda no ciclo da física. Como o puxão mexe na velocidade do
     * Rigidbody, a lógica do balanço (UpdateSwing) fica aqui, e não no Update.
     */
    private void FixedUpdate()
    {
        if (isGrappling)
        {
            UpdateSwing();
        }
    }

    // ---------- Charges ----------

    /*
     * GUIA: HandleChargeRecharge
     * Cuida da recarga. Só começa quando as cargas chegam a zero: trava
     * por "rechargeCooldown" segundos e depois devolve TODAS as cargas de
     * uma vez (não é uma carga por vez). Não faz nada se useCharges for false.
     */
    private void HandleChargeRecharge()
    {
        if (!useCharges || currentCharges > 0) return;

        if (!isRecharging)
        {
            isRecharging = true;
            rechargeTimer = rechargeCooldown;
        }

        rechargeTimer -= Time.deltaTime;

        if (rechargeTimer <= 0f)
        {
            currentCharges = maxCharges;
            isRecharging = false;
        }
    }

    // ---------- Shooting ----------

    /*
     * GUIA: TryShootRope
     * Verifica se pode atirar: precisa ter carga (se useCharges estiver
     * ativo) e não pode já haver uma corda em voo ou grudada. A carga é
     * gasta aqui, ANTES de saber se vai acertar algo, então errar o tiro
     * também gasta a carga.
     */
    private void TryShootRope()
    {
        if (ropeFlying || isGrappling) return;
        if (useCharges && currentCharges <= 0) return;

        if (useCharges) currentCharges--;

        ShootRope();
    }

    /*
     * GUIA: ShootRope
     * Dispara a corda. Trava a origem e a direção no instante do clique
     * (mexer a câmera depois não muda a mira). O acerto é decidido AGORA
     * com um Raycast: se atingir um objeto com uma das tags permitidas,
     * guarda o ponto, a distância e o objeto atingido (com a posição relativa
     * a ele, para a corda seguir o alvo se ele se mover). A corda então
     * "voa" visualmente até esse ponto (ver UpdateRopeFlight).
     */
    private void ShootRope()
    {
        if (hookMesh != null) hookMesh.enabled = true;
        ropeFlying = true;
        ropeLine.enabled = true;

        shootOrigin = ropeOrigin.position;
        shootDirection = ropeOrigin.forward;
        ropeEnd = shootOrigin;

        pendingHit = false;
        hitTransform = null;
        hitCollider = null;

        Ray ray = new Ray(shootOrigin, shootDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, ropeMaxDistance) && IsGrappableTag(hit.collider))
        {
            pendingHit = true;
            pendingHitPoint = hit.point;
            pendingHitDistance = hit.distance;

            hitCollider = hit.collider;
            hitTransform = hit.collider.transform;
            localHitOffset = hitTransform.InverseTransformPoint(hit.point);
        }
    }

    /*
     * GUIA: IsGrappableTag
     * Retorna true se o objeto atingido tem uma das tags da lista
     * "grappleTags". Para permitir agarrar um novo tipo de superfície, é só
     * adicionar a tag dela na lista no Inspector.
     */
    private bool IsGrappableTag(Collider col)
    {
        for (int i = 0; i < grappleTags.Length; i++)
        {
            if (col.CompareTag(grappleTags[i])) return true;
        }

        return false;
    }

    /*
     * GUIA: UpdateRopeFlight
     * Anima a ponta da corda viajando até o destino. É só visual, o resultado
     * já foi decidido no disparo. Se havia um acerto e a corda chegou na
     * distância dele, o gancho gruda (StartGrapple). Se errou e a corda
     * passou do alcance máximo, ela some.
     */
    private void UpdateRopeFlight()
    {
        ropeEnd += shootDirection * ropeSpeed * Time.deltaTime;

        float traveled = Vector3.Distance(shootOrigin, ropeEnd);

        if (pendingHit)
        {
            if (traveled >= pendingHitDistance)
            {
                ropeEnd = pendingHitPoint;
                grapplePoint = pendingHitPoint;
                ropeFlying = false;

                StartGrapple();
            }

            return;
        }

        if (traveled > ropeMaxDistance)
        {
            ropeFlying = false;
            ropeLine.enabled = false;
            if (hookMesh != null) hookMesh.enabled = false;
        }
    }

    // ---------- Grapple ----------

    /*
     * GUIA: StartGrapple
     * Chamado quando a corda chega no alvo. Liga o estado de "grapeando",
     * zera o cronômetro de segurança e define o comprimento inicial da
     * corda como a distância atual até o ponto. Esse comprimento é o
     * "raio" dentro do qual o jogador pode se mover livremente. Também
     * dispara o evento onGrappleStarted.
     */
    private void StartGrapple()
    {
        if (playerRigidbody == null)
        {
            Debug.LogError("GrapplingHook: player Rigidbody not found.", this);
            return;
        }

        isGrappling = true;
        grappleTimer = 0f;
        ropeLength = Vector3.Distance(playerRigidbody.position, grapplePoint);

        onGrappleStarted?.Invoke();
    }

    /*
     * GUIA: UpdateSwing
     * O coração do script, roda a cada passo da física enquanto grudado:
     *  1) Segue o alvo, caso ele se mova.
     *  2) RESTRIÇÃO DA CORDA: se o jogador está além do comprimento da
     *     corda, cancela só a parte da velocidade que aponta PARA FORA.
     *     A parte lateral (tangencial) é preservada, e é ela que cria a
     *     curva do balanço.
     *  3) Aplica o controle leve do jogador (WASD).
     *  4) Aplica o puxão em direção ao ponto do gancho.
     *  5) Encurta a corda aos poucos (recolhimento).
     *  6) Solta automaticamente ao chegar perto do alvo, ou se passar do
     *     tempo máximo de segurança.
     */
    private void UpdateSwing()
    {
        UpdateGrapplePointFromTarget();

        grappleTimer += Time.fixedDeltaTime;

        Vector3 toAnchor = grapplePoint - playerRigidbody.position;
        float distance = toAnchor.magnitude;

        if (distance <= 0.001f)
        {
            ReleaseGrapple(true);
            return;
        }

        Vector3 dirToAnchor = toAnchor / distance;

        if (distance > ropeLength)
        {
            Vector3 vel = playerRigidbody.linearVelocity;
            float outwardSpeed = Vector3.Dot(vel, -dirToAnchor);

            if (outwardSpeed > 0f)
            {
                vel += dirToAnchor * outwardSpeed;
                playerRigidbody.linearVelocity = vel;
            }
        }

        ApplyPlayerSwingControl();

        playerRigidbody.AddForce(dirToAnchor * pullAcceleration, ForceMode.Acceleration);

        ropeLength = Mathf.Max(ropeLength - reelInSpeed * Time.fixedDeltaTime, minRopeLength);

        if (distance <= autoReleaseDistance)
        {
            ReleaseGrapple(IsTouchingTarget());
            return;
        }

        if (grappleTimer >= maxGrappleDuration)
        {
            ReleaseGrapple(false);
        }
    }

    /*
     * GUIA: ApplyPlayerSwingControl
     * Lê o input (WASD) e soma uma força pequena nessa direção, relativa à
     * orientação do jogador. Como é uma força SOMADA (Acceleration), ela
     * nunca substitui a velocidade existente: só "cutuca" a curva do
     * balanço para o lado desejado. Para mudar a facilidade de fazer
     * curvas, ajuste "playerSwingControlForce".
     */
    private void ApplyPlayerSwingControl()
    {
        if (playerSwingControlForce <= 0f) return;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        if (Mathf.Abs(x) < 0.01f && Mathf.Abs(z) < 0.01f) return;

        Transform playerTransform = playerRigidbody.transform;

        Vector3 inputDir = playerTransform.right * x + playerTransform.forward * z;
        inputDir.y = 0f;

        if (inputDir.magnitude > 1f) inputDir.Normalize();

        playerRigidbody.AddForce(inputDir * playerSwingControlForce, ForceMode.Acceleration);
    }

    /*
     * GUIA: IsTouchingTarget
     * Verifica se o jogador realmente encostou na superfície do alvo (e
     * não apenas passou perto). Mede a distância do jogador ao ponto mais
     * próximo do collider do alvo e compara com "collisionCheckRadius".
     * Usado para decidir se o recuo (repel) deve ser aplicado.
     */
    private bool IsTouchingTarget()
    {
        if (hitCollider == null) return false;

        Vector3 closestPoint = hitCollider.ClosestPoint(playerRigidbody.position);
        float distanceToSurface = Vector3.Distance(closestPoint, playerRigidbody.position);

        return distanceToSurface <= collisionCheckRadius;
    }

    /*
     * GUIA: UpdateGrapplePointFromTarget
     * Recalcula o ponto de gancho a partir do objeto atingido, usando a
     * posição relativa guardada no disparo. Assim, se o alvo se mover ou
     * girar (plataforma móvel, por exemplo), a corda continua presa no
     * mesmo ponto dele.
     */
    private void UpdateGrapplePointFromTarget()
    {
        if (hitTransform != null)
        {
            grapplePoint = hitTransform.TransformPoint(localHitOffset);
        }
    }

    // ---------- Release ----------

    /*
     * GUIA: Release
     * Solta o gancho manualmente (sem recuo). Também cancela uma corda que
     * ainda esteja voando. Pode ser chamado de outros scripts, por exemplo
     * quando o jogador morre ou toma dano.
     */
    public void Release()
    {
        if (ropeFlying || isGrappling)
        {
            ReleaseGrapple(false);
        }
    }

    /*
     * GUIA: ReleaseGrapple
     * Finaliza o gancho: desliga o estado de grapple, esconde a corda e a
     * ponta e limpa o alvo. A velocidade do jogador NÃO é alterada, ele
     * sai voando com todo o momentum do balanço. Se "collided" for true
     * (e o recuo estiver ativo), aplica o recuo. Por fim, dispara o evento
     * onGrappleReleased.
     */
    private void ReleaseGrapple(bool collided)
    {
        bool wasGrappling = isGrappling;

        isGrappling = false;
        ropeFlying = false;

        ropeLine.enabled = false;
        if (hookMesh != null) hookMesh.enabled = false;

        Vector3 travelDirection = playerRigidbody.linearVelocity.sqrMagnitude > 0.01f
            ? playerRigidbody.linearVelocity.normalized
            : (grapplePoint - playerRigidbody.position).normalized;

        hitTransform = null;
        hitCollider = null;

        if (collided && enableRepel)
        {
            ApplyRepel(travelDirection);
        }

        if (wasGrappling)
        {
            onGrappleReleased?.Invoke();
        }
    }

    /*
     * GUIA: ApplyRepel
     * Aplica um impulso de recuo, para trás (oposto à direção que o jogador
     * vinha) e um pouco para cima, simulando o "tranco" de bater no alvo.
     * Se o alvo estava acima, usa o impulso vertical maior
     * (repelUpwardBoost); senão usa o menor (repelMinUpward). A velocidade
     * horizontal é zerada antes do impulso, para o recuo ser consistente.
     * "forceMultiplier" permite diminuir ou aumentar a força (1 = normal).
     */
    public void ApplyRepel(Vector3 travelDirection, float forceMultiplier = 1f)
    {
        if (playerRigidbody == null) return;

        Vector3 flatBack = new Vector3(-travelDirection.x, 0f, -travelDirection.z);
        if (flatBack.sqrMagnitude < 0.0001f)
        {
            flatBack = -playerRigidbody.transform.forward;
        }

        flatBack.Normalize();

        float verticalAmount = travelDirection.y > upwardTargetThreshold
            ? repelUpwardBoost
            : repelMinUpward;

        Vector3 finalImpulse = (flatBack * repelForce + Vector3.up * verticalAmount) * forceMultiplier;

        Vector3 currentVel = playerRigidbody.linearVelocity;
        playerRigidbody.linearVelocity = new Vector3(0f, currentVel.y, 0f);

        playerRigidbody.AddForce(finalImpulse, ForceMode.VelocityChange);
    }

    // ---------- Visual ----------

    /*
     * GUIA: UpdateRopeVisual
     * Atualiza os dois pontos do LineRenderer a cada frame. O primeiro
     * fica sempre na origem da corda (acompanha a mão). O segundo fica na
     * ponta voando (enquanto a corda está em voo) ou no ponto de gancho
     * (quando já está grudada).
     */
    private void UpdateRopeVisual()
    {
        if (!ropeLine.enabled) return;

        ropeLine.SetPosition(0, ropeOrigin.position);
        ropeLine.SetPosition(1, ropeFlying ? ropeEnd : grapplePoint);
    }
}