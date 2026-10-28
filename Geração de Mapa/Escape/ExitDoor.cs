/*
 * ============================================================================
 *  EXIT DOOR  -  Porta de saída (acompanha o RoomGenerator)
 * ============================================================================
 *
 *  Criado por: Acervo Gambiarra  |  Código 100% GRATUITO.
 *  Você pode apagar todos os blocos de comentário deste arquivo se quiser.
 *
 *  O QUE FAZ
 *  ---------
 *  Esta é a porta que liga uma sala à próxima. Quando o jogador a abre
 *  (encostando nela ou por interação), ela avisa o RoomGenerator para gerar
 *  a próxima sala e desativa a si mesma.
 *
 *  COMO USAR
 *  ---------
 *  1. Adicione este componente no prefab da porta (na raiz ou em um filho,
 *     por exemplo no objeto que tem o Collider).
 *  2. Coloque a tag "Player" no seu jogador (ou mude o campo "playerTag").
 *  3. O RoomGenerator chama Initialize() automaticamente ao criar a porta.
 *  4. Para abrir com tecla (ex.: E) em vez de encostar, desmarque
 *     "openOnContact" e chame Interact() no script do seu jogador.
 *
 *  PONTOS IMPORTANTES
 *  ------------------
 *  - É preciso ter um Collider na porta. Para detectar por trigger, marque
 *    "Is Trigger"; para colisão normal, o jogador precisa de um Rigidbody
 *    ou CharacterController.
 *  - O nome do arquivo precisa ser ExitDoor.cs (igual ao nome da classe).
 * ============================================================================
 */

using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    [Tooltip("Tag of the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If true, touching the door opens it automatically. If false, call Interact() from your player script.")]
    [SerializeField] private bool openOnContact = true;

    private RoomGenerator generator;
    private bool opened = false;

    /*
     * GUIA: Initialize
     * Recebe o RoomGenerator que criou esta porta e guarda a referência.
     * É chamado automaticamente pelo gerador logo após criar a porta; sem
     * isso, a porta não sabe quem deve gerar a próxima sala.
     */
    public void Initialize(RoomGenerator roomGenerator)
    {
        generator = roomGenerator;
    }

    /*
     * GUIA: Interact
     * Abre a porta: pede ao gerador a próxima sala e desativa este objeto.
     * Só funciona uma vez (flag "opened") e só se o gerador existir.
     * Pode ser chamado pelo script do jogador (ex.: raycast + tecla).
     * Desativa apenas este objeto, não a raiz "Door" do prefab.
     */
    public void Interact()
    {
        if (opened || generator == null) return;

        opened = true;
        generator.NextRoom();

        gameObject.SetActive(false);
    }

    /*
     * GUIA: OnCollisionEnter
     * Chamado pela Unity quando algo colide com a porta. Se "openOnContact"
     * estiver ativo e o objeto tiver a tag do jogador, abre a porta.
     */
    private void OnCollisionEnter(Collision collision)
    {
        if (openOnContact && collision.gameObject.CompareTag(playerTag))
        {
            Interact();
        }
    }

    /*
     * GUIA: OnTriggerEnter
     * Igual ao OnCollisionEnter, mas para quando o Collider da porta está
     * marcado como "Is Trigger" (o jogador atravessa em vez de bater).
     */
    private void OnTriggerEnter(Collider other)
    {
        if (openOnContact && other.CompareTag(playerTag))
        {
            Interact();
        }
    }
}