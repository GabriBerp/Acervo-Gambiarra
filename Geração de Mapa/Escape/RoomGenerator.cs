/*
 * ============================================================================
 *  PROCEDURAL ROOM GENERATOR  -  Versão Base
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
 *  Gera salas proceduralmente, uma depois da outra, a partir de imagens
 *  pixel-art (Texture2D). Cada pixel da imagem vira um bloco no mundo 3D,
 *  dependendo da COR do pixel (parede, chão, vazio, início, saída).
 *  A SAÍDA de uma sala é ligada ao INÍCIO da próxima, e a próxima sala é
 *  girada automaticamente (em múltiplos de 90°) para encaixar direitinho.
 *  Assim você consegue criar jogos de corredores infinitos / salas em
 *  sequência, no estilo de Doors, Pressure, Grace e similares.
 *
 *  RECURSOS PRINCIPAIS
 *  -------------------
 *  - Salas com MÚLTIPLOS ANDARES (andar 0 + andares extras com alturas
 *    diferentes).
 *  - CONTROLE DE DIFICULDADE: cada sala tem dificuldade de 1 a 4 e só
 *    aparece quando a dificuldade atual do jogo for igual ou maior.
 *  - LIMITE POR PROGRESSO: cada sala pode ter "minRoomToAppear" e
 *    "maxRoomToAppear" para só ser gerada depois de X salas (ou até X salas).
 *  - Descarrega salas antigas automaticamente para economizar memória.
 *
 *  COMO USAR (PASSO A PASSO)
 *  -------------------------
 *  1. Crie um GameObject vazio na cena e adicione este script (RoomGenerator).
 *  2. Crie os prefabs: chão (ground), parede (wall), vazio (void),
 *     porta de saída (exit door) e um objeto vazio para servir de "pai" de
 *     cada sala (room prefab). Arraste todos nos campos do Inspector.
 *  3. A porta de saída precisa ter o componente ExitDoor (arquivo
 *     ExitDoor.cs), no objeto raiz ou em algum filho.
 *  4. Desenhe as salas em pixel-art. Em CADA imagem, ative "Read/Write" nas
 *     configurações de importação e use Filter Mode = Point (no filter),
 *     Compression = None para as cores não ficarem alteradas.
 *  5. Pinte os pixels com as cores configuradas no Inspector:
 *     branco = parede, cinza = chão, verde = início, vermelho = saída,
 *     amarelo = vazio (mata o jogador). Qualquer outra cor é ignorada.
 *  6. Adicione as imagens na lista "Room Templates" e defina a dificuldade
 *     de cada uma (e, se quiser, quando ela pode aparecer).
 *  7. Dê Play! Ao encostar na porta de saída, a próxima sala é gerada.
 *
 *  PONTOS IMPORTANTES AO ADAPTAR PARA O SEU PROJETO
 *  ------------------------------------------------
 *  - "blockSize" deve ser igual ao tamanho do seu prefab de chão/parede.
 *  - "doorHeightOffset" e "doorYawOffset" ajustam a altura e a rotação da
 *    porta, dependendo de onde fica o pivô do seu modelo.
 *  - O pixel de INÍCIO deve ficar na borda da imagem, onde a sala anterior
 *    se conecta. O mesmo vale para a SAÍDA.
 *  - Use startEdge / exitEdge = Auto para o script descobrir a borda mais
 *    próxima, ou escolha manualmente se a sala tiver formato diferente.
 *  - Todos os andares extras devem estar alinhados com o andar 0 pelo
 *    pixel (0,0).
 *  - O jogador precisa ter a tag configurada em ExitDoor (padrão "Player").
 *  - As salas NÃO podem ficar sobrepostas: desenhe de modo que a saída
 *    leve para um espaço livre.
 *  - Aumente "maxLoadedRooms" se o jogador puder voltar para salas antigas.
 *
 *  Obs.: o nome da classe precisa ser igual ao nome do arquivo (RoomGenerator.cs).
 * ============================================================================
 */

using System.Collections.Generic;
using UnityEngine;

/*
 * GUIA: RoomEdge
 * Indica qual borda da imagem da sala contém o início ou a saída.
 * "Auto" faz o script detectar sozinho a borda mais próxima do pixel.
 * Os demais valores (Top, Right, Bottom, Left) forçam uma borda específica.
 */
public enum RoomEdge { Auto = 0, Top = 1, Right = 2, Bottom = 3, Left = 4 }

/*
 * GUIA: ExtraFloor
 * Representa um andar EXTRA de uma sala (andar 1, 2, 3...).
 * O andar 0 é sempre a "roomTexture" do RoomTemplate.
 * Cada andar extra tem sua própria imagem e uma altura (em blocos) em
 * relação ao andar anterior. Deve estar alinhado ao andar 0 pelo pixel (0,0).
 */
[System.Serializable]
public class ExtraFloor
{
    [Tooltip("Pixel-art of this floor. Aligned with floor 0 by pixel (0,0). Read/Write must be enabled.")]
    public Texture2D floorTexture;

    [Tooltip("Height difference, in BLOCKS (blockSize), relative to the PREVIOUS floor.")]
    public float heightFromPreviousFloor = 2f;
}

/*
 * GUIA: RoomTemplate
 * Modelo de uma sala que o gerador pode sortear. Guarda a imagem principal
 * (andar 0), a dificuldade (1 a 4), em quais salas ela pode aparecer,
 * como achar o ponto de início, em quais bordas ficam início/saída e a
 * lista de andares extras. Aparece no Inspector graças ao [Serializable].
 */
[System.Serializable]
public class RoomTemplate
{
    [Tooltip("Pixel-art map of the room (any size). Read/Write must be enabled. This is FLOOR 0.")]
    public Texture2D roomTexture;

    [Range(1, 4)]
    [Tooltip("Difficulty of this room, from 1 to 4. It only appears when the current difficulty is equal or higher.")]
    public int difficulty = 1;

    [Header("Progress Limits")]
    [Tooltip("This room only starts appearing from this room number on. 0 or 1 = can appear from the start.")]
    public int minRoomToAppear = 0;

    [Tooltip("This room stops appearing after this room number. 0 = no limit.")]
    public int maxRoomToAppear = 0;

    [Header("Start Point")]
    [Tooltip("If true, the start point is the pixel with the 'Start' color. If false, uses startPixel below.")]
    public bool useColorForStartPoint = true;

    [Tooltip("Start pixel (x, y), used when useColorForStartPoint is false.")]
    public Vector2Int startPixel = Vector2Int.zero;

    [Header("Edges (room direction)")]
    [Tooltip("Which edge of the texture holds the START. Auto = closest edge to the pixel.")]
    public RoomEdge startEdge = RoomEdge.Auto;

    [Tooltip("Which edge of the texture holds the EXIT. Auto = closest edge to the pixel.")]
    public RoomEdge exitEdge = RoomEdge.Auto;

    [Header("Multiple Floors")]
    [Tooltip("If true, uses the 'extraFloors' list below to build floors above floor 0.")]
    public bool hasMultipleFloors = false;

    [Tooltip("Extra floors in order (floor 1, floor 2...). Each has its own image and a height relative to the previous one.")]
    public List<ExtraFloor> extraFloors = new List<ExtraFloor>();
}

/*
 * GUIA: RoomGenerator
 * Classe principal. Controla o número da sala atual, calcula a dificuldade,
 * sorteia o modelo de sala, constrói os blocos 3D a partir dos pixels,
 * conecta a saída da sala anterior ao início da nova e remove salas antigas.
 */
public class RoomGenerator : MonoBehaviour
{
    [Header("Room Templates")]
    [Tooltip("Room floor plans. Each one has its own difficulty (1-4) and progress limits.")]
    [SerializeField] private List<RoomTemplate> roomTemplates = new List<RoomTemplate>();

    [Header("Prefabs")]
    [SerializeField] private GameObject groundPrefab;   // floor tile
    [SerializeField] private GameObject wallPrefab;     // wall block
    [SerializeField] private GameObject exitDoorPrefab; // door that connects rooms (needs an ExitDoor component)
    [SerializeField] private GameObject voidPrefab;     // void pits that kill the player
    [SerializeField] private GameObject roomPrefab;     // empty parent object for each room

    [Header("Grid Config")]
    [SerializeField] private float blockSize = 2.0f;

    [Header("Door Config")]
    [Tooltip("Yaw adjustment (degrees on Y) in case the door prefab does not face +Z by default.")]
    [SerializeField] private float doorYawOffset = 0f;

    [Tooltip("Height added to the door position (useful when the door pivot is at its center).")]
    [SerializeField] private float doorHeightOffset = 2.24f;

    [Header("Pixel Color Codes")]
    [SerializeField] private Color wallColor = Color.white;
    [SerializeField] private Color groundColor = Color.gray;
    [SerializeField] private Color startColor = Color.green; // where this room connects to the previous one
    [SerializeField] private Color exitColor = Color.red;    // where the exit door spawns
    [SerializeField] private Color voidColor = Color.yellow; // where the void spawns
    [SerializeField, Range(0f, 0.5f)] private float colorTolerance = 0.1f;

    [Header("Room Number Config")]
    public int roomNumber = 1;
    [SerializeField] private int totalRooms = 100;
    [SerializeField] private int maxDifficulty = 4;
    [Tooltip("How many rooms stay loaded at once (older ones are destroyed).")]
    [SerializeField] private int maxLoadedRooms = 2;

    // Directions: 0 = Top(+Z), 1 = Right(+X), 2 = Bottom(-Z), 3 = Left(-X)  (clockwise seen from above)

    // World position where the START point of the next room must be placed
    private Vector3 nextEntryPosition;
    // World direction in which the next room must grow (direction of the last exit)
    private int nextExitWorldDirection = 0;
    private bool hasPreviousExit = false;
    private readonly Queue<GameObject> loadedRooms = new Queue<GameObject>();

    /*
     * GUIA: CurrentDifficulty
     * Propriedade que calcula a dificuldade atual do jogo (1 até maxDifficulty).
     * Divide o número da sala atual pelo total de salas para obter o progresso
     * (0 a 1) e multiplica pela dificuldade máxima. Quanto mais o jogador
     * avança, maior a dificuldade, o que libera modelos de sala mais difíceis.
     */
    public int CurrentDifficulty
    {
        get
        {
            float progress = Mathf.Clamp01((float)roomNumber / totalRooms);
            return Mathf.Clamp(Mathf.CeilToInt(progress * maxDifficulty), 1, maxDifficulty);
        }
    }

    /*
     * GUIA: Start
     * Executa uma vez quando o jogo começa. Define a posição inicial de
     * conexão como a posição deste objeto, zera as informações de saída
     * anterior e gera a primeira sala (que nunca é rotacionada).
     */
    private void Start()
    {
        nextEntryPosition = transform.position;
        nextExitWorldDirection = 0;
        hasPreviousExit = false;
        GenerateRoom();
    }

    /*
     * GUIA: NextRoom
     * Chamada pela porta de saída (ExitDoor) quando o jogador a abre.
     * Aumenta o número da sala em 1 e gera a próxima sala da sequência.
     * Você também pode chamar este método de outros scripts, se precisar.
     */
    public void NextRoom()
    {
        roomNumber++;
        GenerateRoom();
    }

    /*
     * GUIA: SelectRandomRoomTemplate
     * Sorteia um modelo de sala permitido para o momento atual do jogo.
     * Uma sala só entra no sorteio se: tiver imagem; sua dificuldade for
     * menor ou igual à dificuldade atual; o número da sala atual for maior
     * ou igual a "minRoomToAppear"; e, se "maxRoomToAppear" for maior que 0,
     * o número da sala atual não ultrapassar esse limite.
     * Retorna null (com erro no Console) se nenhuma sala estiver disponível.
     */
    public RoomTemplate SelectRandomRoomTemplate()
    {
        int difficulty = CurrentDifficulty;
        List<RoomTemplate> available = new List<RoomTemplate>();

        foreach (RoomTemplate template in roomTemplates)
        {
            if (template == null || template.roomTexture == null) continue;
            if (template.difficulty > difficulty) continue;
            if (roomNumber < template.minRoomToAppear) continue;
            if (template.maxRoomToAppear > 0 && roomNumber > template.maxRoomToAppear) continue;

            available.Add(template);
        }

        if (available.Count == 0)
        {
            Debug.LogError($"No RoomTemplate available for difficulty {difficulty} at room {roomNumber}.", this);
            return null;
        }

        return available[Random.Range(0, available.Count)];
    }

    /*
     * GUIA: GenerateRoom
     * Método principal de geração. Passos:
     *  0) Monta a lista de andares (imagem + altura de cada um).
     *  1) Acha o pixel de início (andar 0) e o pixel de saída (qualquer andar).
     *  2) Descobre as bordas do início e da saída.
     *  3) Calcula a rotação (múltiplos de 90°) para o início da nova sala
     *     ficar de costas para a saída da sala anterior.
     *  4) Cria o objeto da sala na posição correta, alinhando início e saída.
     *  5) Percorre todos os pixels de todos os andares e cria os blocos.
     *  6) Guarda a posição e a direção da saída para a próxima sala.
     *  7) Destrói as salas mais antigas que passarem de "maxLoadedRooms".
     */
    public void GenerateRoom()
    {
        RoomTemplate template = SelectRandomRoomTemplate();
        if (template == null) return;

        // 0) Build the floor list: (texture, local Y height in world units)
        List<Texture2D> floorMaps = new List<Texture2D>();
        List<float> floorHeights = new List<float>();

        floorMaps.Add(template.roomTexture);
        floorHeights.Add(0f);

        if (template.hasMultipleFloors && template.extraFloors != null)
        {
            float accumulatedY = 0f;
            for (int i = 0; i < template.extraFloors.Count; i++)
            {
                ExtraFloor extra = template.extraFloors[i];
                if (extra == null || extra.floorTexture == null)
                {
                    Debug.LogWarning($"Room '{template.roomTexture.name}': extra floor {i + 1} has no texture, skipped.", this);
                    continue;
                }

                accumulatedY += extra.heightFromPreviousFloor * blockSize;
                floorMaps.Add(extra.floorTexture);
                floorHeights.Add(accumulatedY);
            }
        }

        Texture2D baseMap = floorMaps[0];

        // 1) Start point: always on floor 0
        if (!FindStartPixel(template, baseMap, out Vector2Int startPixel))
        {
            Debug.LogError($"Room '{baseMap.name}' has no start point (color or startPixel).", this);
            return;
        }

        // Exit: searched in ALL floors (first one found)
        bool hasExit = false;
        Vector2Int exitPixel = Vector2Int.zero;
        int exitFloor = 0;
        for (int f = 0; f < floorMaps.Count; f++)
        {
            if (FindPixelByColor(floorMaps[f], exitColor, out exitPixel))
            {
                hasExit = true;
                exitFloor = f;
                break;
            }
        }

        if (!hasExit)
        {
            Debug.LogWarning($"Room '{baseMap.name}' has no exit pixel; the chain will end here.", this);
        }

        // 2) Texture edges for start and exit
        int startEdgeIndex = ResolveEdge(template.startEdge, startPixel, baseMap);
        int exitEdgeIndex = hasExit ? ResolveEdge(template.exitEdge, exitPixel, baseMap) : 0;

        // 3) Room rotation (k * 90°)
        //    The start edge must face away from the direction the previous room exited.
        //    The first room is not rotated.
        int k = hasPreviousExit
            ? Mod(nextExitWorldDirection + 2 - startEdgeIndex, 4)
            : 0;
        Quaternion roomRotation = Quaternion.Euler(0f, 90f * k, 0f);

        // 4) Place the room so its start pixel lines up with the previous room's exit
        Vector3 roomOrigin = nextEntryPosition - roomRotation * PixelToLocal(startPixel);
        GameObject roomObject = Instantiate(roomPrefab, roomOrigin, roomRotation, transform);
        roomObject.name = $"Room {roomNumber} ({baseMap.name}, {floorMaps.Count} floor(s), rot {k * 90}°)";

        // 5) Build each floor (everything in LOCAL coordinates of the already rotated room)
        for (int f = 0; f < floorMaps.Count; f++)
        {
            Texture2D map = floorMaps[f];
            Vector3 floorOffset = Vector3.up * floorHeights[f];

            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    Color pixel = map.GetPixel(x, y);
                    Vector3 localPos = floorOffset + PixelToLocal(new Vector2Int(x, y));

                    bool isStart = (f == 0 && x == startPixel.x && y == startPixel.y);
                    bool isExit = hasExit && f == exitFloor && x == exitPixel.x && y == exitPixel.y;

                    if (isExit)
                    {
                        SpawnObject(groundPrefab, localPos, roomObject.transform);
                        SpawnExitDoor(localPos, exitEdgeIndex, roomObject.transform);
                    }
                    else if (isStart)
                    {
                        // If there was a previous room, its door tile already covers this cell.
                        if (!hasPreviousExit)
                        {
                            SpawnObject(groundPrefab, localPos, roomObject.transform);
                        }
                    }
                    else
                    {
                        CheckAndSpawn(pixel, localPos, roomObject.transform);
                    }
                }
            }
        }

        // 6) Prepare the connection for the next room
        if (hasExit)
        {
            Vector3 exitLocal = Vector3.up * floorHeights[exitFloor] + PixelToLocal(exitPixel);
            nextEntryPosition = roomObject.transform.TransformPoint(exitLocal);
            nextExitWorldDirection = Mod(exitEdgeIndex + k, 4);
            hasPreviousExit = true;
        }

        // 7) Unload old rooms
        loadedRooms.Enqueue(roomObject);
        while (loadedRooms.Count > maxLoadedRooms)
        {
            Destroy(loadedRooms.Dequeue());
        }
    }

    // ---------- Helpers ----------

    /*
     * GUIA: Mod
     * Módulo matemático que nunca retorna número negativo (o operador % do C#
     * pode retornar negativos). Usado para manter as direções sempre entre
     * 0 e 3, mesmo quando a conta dá um valor menor que zero.
     */
    private static int Mod(int a, int m)
    {
        return ((a % m) + m) % m;
    }

    /*
     * GUIA: PixelToLocal
     * Converte a posição de um pixel (x, y) da imagem para uma posição 3D
     * local da sala. O X do pixel vira X no mundo, o Y do pixel vira Z no
     * mundo, e a altura fica em 0. Tudo é multiplicado por "blockSize".
     */
    private Vector3 PixelToLocal(Vector2Int pixel)
    {
        return new Vector3(pixel.x * blockSize, 0f, pixel.y * blockSize);
    }

    /*
     * GUIA: ResolveEdge
     * Descobre qual borda (0 = Top, 1 = Right, 2 = Bottom, 3 = Left) deve
     * ser usada. Se a borda foi escolhida manualmente, apenas converte o enum
     * em número. Se estiver em Auto, calcula a distância do pixel até cada
     * uma das quatro bordas da imagem e escolhe a mais próxima.
     */
    private int ResolveEdge(RoomEdge manual, Vector2Int pixel, Texture2D map)
    {
        if (manual != RoomEdge.Auto)
        {
            return (int)manual - 1; // Top=0, Right=1, Bottom=2, Left=3
        }

        int top = map.height - 1 - pixel.y;
        int right = map.width - 1 - pixel.x;
        int bottom = pixel.y;
        int left = pixel.x;

        int best = 0;
        int bestDistance = top;
        if (right < bestDistance) { best = 1; bestDistance = right; }
        if (bottom < bestDistance) { best = 2; bestDistance = bottom; }
        if (left < bestDistance) { best = 3; bestDistance = left; }
        return best;
    }

    /*
     * GUIA: FindStartPixel
     * Encontra o pixel de início da sala. Se "useColorForStartPoint" estiver
     * ativo, procura na imagem o pixel com a cor de início. Caso contrário,
     * usa o "startPixel" informado manualmente, verificando se ele está dentro
     * dos limites da imagem. Retorna false se não encontrar um início válido.
     */
    private bool FindStartPixel(RoomTemplate template, Texture2D map, out Vector2Int result)
    {
        if (template.useColorForStartPoint)
        {
            return FindPixelByColor(map, startColor, out result);
        }

        result = template.startPixel;
        return result.x >= 0 && result.x < map.width && result.y >= 0 && result.y < map.height;
    }

    /*
     * GUIA: FindPixelByColor
     * Varre a imagem pixel por pixel e devolve a posição do primeiro pixel
     * cuja cor seja parecida (dentro da tolerância) com a cor procurada.
     * Retorna true se achou, false se não existir nenhum pixel dessa cor.
     */
    private bool FindPixelByColor(Texture2D map, Color target, out Vector2Int result)
    {
        for (int x = 0; x < map.width; x++)
        {
            for (int y = 0; y < map.height; y++)
            {
                if (CompareColors(map.GetPixel(x, y), target))
                {
                    result = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        result = Vector2Int.zero;
        return false;
    }

    /*
     * GUIA: CheckAndSpawn
     * Olha a cor de um pixel e cria o objeto correspondente na posição local
     * indicada: parede, chão ou vazio. Pixels com outras cores (como
     * transparente ou preto) são ignorados e não geram nada.
     * Para adicionar novos tipos de bloco, crie uma cor e um prefab novos
     * e inclua mais um "else if" aqui.
     */
    private void CheckAndSpawn(Color color, Vector3 localPos, Transform parent)
    {
        if (CompareColors(color, wallColor))
        {
            SpawnObject(wallPrefab, localPos, parent);
        }
        else if (CompareColors(color, groundColor))
        {
            SpawnObject(groundPrefab, localPos, parent);
        }
        else if (CompareColors(color, voidColor))
        {
            SpawnObject(voidPrefab, localPos, parent);
        }
    }

    /*
     * GUIA: CompareColors
     * Compara duas cores com uma pequena tolerância (colorTolerance).
     * Isso evita erros quando duas cores são quase idênticas por causa de
     * compressão da imagem ou arredondamentos. Compara só R, G e B
     * (o alfa é ignorado).
     */
    private bool CompareColors(Color c1, Color c2)
    {
        return Mathf.Abs(c1.r - c2.r) < colorTolerance &&
               Mathf.Abs(c1.g - c2.g) < colorTolerance &&
               Mathf.Abs(c1.b - c2.b) < colorTolerance;
    }

    /*
     * GUIA: SpawnExitDoor
     * Cria a porta de saída na posição local indicada, elevada por
     * "doorHeightOffset" e girada para olhar para a borda onde está a saída
     * (somando o "doorYawOffset"). Depois procura o componente ExitDoor no
     * objeto ou em seus filhos e o inicializa com este gerador, para que a
     * porta consiga pedir a próxima sala. Avisa no Console se não achar o
     * componente.
     */
    private void SpawnExitDoor(Vector3 localPos, int exitEdgeIndex, Transform parent)
    {
        if (exitDoorPrefab == null) return;

        // The door faces the edge where the exit is (Top = identity)
        GameObject door = Instantiate(exitDoorPrefab, parent);
        door.transform.localPosition = localPos + (Vector3.up * doorHeightOffset);
        door.transform.localRotation = Quaternion.Euler(0f, 90f * exitEdgeIndex + doorYawOffset, 0f);

        // The ExitDoor component can be on the root or on a child (e.g. Door > Cube)
        ExitDoor exitDoor = door.GetComponentInChildren<ExitDoor>(true);
        if (exitDoor != null)
        {
            exitDoor.Initialize(this);
        }
        else
        {
            Debug.LogWarning("exitDoorPrefab has no ExitDoor component (root or children).", door);
        }
    }

    /*
     * GUIA: SpawnObject
     * Cria um prefab como filho do objeto "parent", na posição local
     * informada e sem rotação. Se o prefab não tiver sido definido no
     * Inspector, não faz nada (evita erros de referência nula).
     */
    private void SpawnObject(GameObject prefab, Vector3 localPos, Transform parent)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, parent);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.identity;
    }
}