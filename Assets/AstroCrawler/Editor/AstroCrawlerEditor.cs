// AstroCrawler - the designer-facing balancing window.
//
// Adapted from CCG Kit's CCGKitEditor: same idea of a tabbed IMGUI window that
// round-trips a folder of JSON through the runtime data classes, so balancing
// never requires recompiling. The tabs are AstroCrawler's own - cards with real
// time durations, ships with a 15-card deck, enemies, and the twenty waves.

using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;

namespace AstroCrawler
{
    public class AstroCrawlerEditor : EditorWindow
    {
        private const string PathKey = "AstroCrawler_ConfigPath";

        private static readonly string[] TabNames = { "Cartas", "Naves", "Inimigos", "Ondas", "Validação" };

        private GameConfig config;
        private string configPath;
        private int tab;
        private Vector2 scroll;

        private readonly HashSet<string> expandedCards = new HashSet<string>();
        private readonly HashSet<int> expandedWaves = new HashSet<int>();
        private List<string> validation;

        [MenuItem("Tools/AstroCrawler/Editor de Conteúdo", false, 0)]
        private static void Open()
        {
            var window = GetWindow(typeof(AstroCrawlerEditor));
            window.titleContent = new GUIContent("AstroCrawler");
            window.minSize = new Vector2(720f, 460f);
        }

        [MenuItem("Tools/AstroCrawler/Criar cena do jogo", false, 20)]
        private static void CreateGameScene()
        {
            AstroCrawlerSceneBuilder.CreateScene();
        }

        private static string DefaultPath
        {
            get { return Path.Combine(Application.dataPath, "AstroCrawler/Resources/AstroCrawler"); }
        }

        private void OnEnable()
        {
            configPath = EditorPrefs.GetString(PathKey, DefaultPath);
            Reload();
        }

        private void Reload()
        {
            if (!Directory.Exists(configPath))
            {
                configPath = DefaultPath;
            }
            config = GameConfig.LoadFromFolder(configPath);
            validation = null;
        }

        private void Save()
        {
            config.SaveToFolder(configPath);
            EditorPrefs.SetString(PathKey, configPath);
            validation = config.Validate();
            Debug.Log("[AstroCrawler] Conteúdo salvo em " + configPath);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Nenhuma configuração carregada.", MessageType.Warning);
                return;
            }

            tab = GUILayout.Toolbar(tab, TabNames);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawCards(); break;
                case 1: DrawShips(); break;
                case 2: DrawEnemies(); break;
                case 3: DrawWaves(); break;
                default: DrawValidation(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Recarregar", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                Reload();
            }
            if (GUILayout.Button("Salvar", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                Save();
            }
            if (GUILayout.Button("Pasta...", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                var picked = EditorUtility.OpenFolderPanel("Pasta de configuração", configPath, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    configPath = picked;
                    Reload();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(configPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------
        // Cards
        // ------------------------------------------------------------------

        private void DrawCards()
        {
            EditorGUILayout.LabelField(config.cards.Count + " cartas", EditorStyles.boldLabel);

            for (var i = 0; i < config.cards.Count; i++)
            {
                var card = config.cards[i];
                var key = card.id + "#" + i;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                var expanded = expandedCards.Contains(key);
                if (GUILayout.Button(expanded ? "▼" : "▶", GUILayout.Width(26f)))
                {
                    if (expanded) expandedCards.Remove(key);
                    else expandedCards.Add(key);
                }

                EditorGUILayout.LabelField(card.name, EditorStyles.boldLabel, GUILayout.Width(220f));
                EditorGUILayout.LabelField(card.category.ToString(), GUILayout.Width(90f));
                EditorGUILayout.LabelField("→ " + card.evolvedName + " → " + card.maxName, EditorStyles.miniLabel);

                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    config.cards.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                if (expandedCards.Contains(key))
                {
                    EditorGUI.indentLevel++;
                    card.id = EditorGUILayout.TextField("Id", card.id);
                    card.name = EditorGUILayout.TextField("Nome", card.name);
                    card.description = EditorGUILayout.TextField("Descrição", card.description);
                    card.evolvedName = EditorGUILayout.TextField("Nome evoluído", card.evolvedName);
                    card.evolvedDescription =
                        EditorGUILayout.TextField("Descrição evoluída", card.evolvedDescription);
                    card.maxName = EditorGUILayout.TextField("Nome nível 3", card.maxName);
                    card.maxDescription =
                        EditorGUILayout.TextField("Descrição nível 3", card.maxDescription);
                    card.category = (CardCategory)EditorGUILayout.EnumPopup("Categoria", card.category);
                    card.effect = (CardEffectId)EditorGUILayout.EnumPopup("Efeito", card.effect);
                    card.tint = EditorGUILayout.TextField("Cor (RRGGBB)", card.tint);

                    EditorGUILayout.Space();
                    DrawParams("Parâmetros base", card.baseParams);
                    DrawParams("Parâmetros evoluídos", card.evolvedParams);
                    DrawParams("Parâmetros nível 3", card.maxParams);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Adicionar carta"))
            {
                config.cards.Add(new CardDefinition { id = "nova_carta", name = "Nova Carta" });
            }
        }

        private void DrawParams(string title, ParamSet set)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            for (var i = 0; i < set.entries.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                set.entries[i].key = EditorGUILayout.TextField(set.entries[i].key, GUILayout.Width(200f));
                set.entries[i].value = EditorGUILayout.FloatField(set.entries[i].value, GUILayout.Width(90f));
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    set.entries.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ parâmetro", GUILayout.Width(120f)))
            {
                set.entries.Add(new ParamEntry { key = "duration", value = 8f });
            }
        }

        // ------------------------------------------------------------------
        // Ships
        // ------------------------------------------------------------------

        private void DrawShips()
        {
            var cardIds = CardIds();

            foreach (var ship in config.ships)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var size = ship.DeckSize();
                var header = ship.name + "   ·   deck " + size + " cartas únicas";
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

                ship.id = EditorGUILayout.TextField("Id", ship.id);
                ship.name = EditorGUILayout.TextField("Nome", ship.name);
                ship.tagline = EditorGUILayout.TextField("Frase", ship.tagline);
                ship.identity = EditorGUILayout.TextField("Identidade", ship.identity);
                ship.maxHp = EditorGUILayout.IntField("HP", ship.maxHp);
                ship.fireInterval = EditorGUILayout.FloatField("Intervalo de tiro (s)", ship.fireInterval);
                ship.damagePerShot = EditorGUILayout.FloatField("Dano por tiro", ship.damagePerShot);
                ship.baseProjectiles = EditorGUILayout.IntField("Projéteis base", ship.baseProjectiles);
                ship.tint = EditorGUILayout.TextField("Cor (RRGGBB)", ship.tint);

                EditorGUILayout.LabelField("Deck padrão", EditorStyles.miniBoldLabel);
                for (var i = 0; i < ship.deck.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    var index = Mathf.Max(0, cardIds.IndexOf(ship.deck[i].cardId));
                    var picked = EditorGUILayout.Popup(index, cardIds.ToArray(), GUILayout.Width(220f));
                    if (picked >= 0 && picked < cardIds.Count)
                    {
                        var pickedId = cardIds[picked];
                        var alreadyUsed = false;
                        for (var other = 0; other < ship.deck.Count; other++)
                            if (other != i && ship.deck[other].cardId == pickedId) alreadyUsed = true;
                        if (!alreadyUsed) ship.deck[i].cardId = pickedId;
                    }

                    var card = config.GetCard(ship.deck[i].cardId);
                    EditorGUILayout.LabelField(card != null ? card.name : "<desconhecida>",
                        EditorStyles.miniLabel);

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        ship.deck.RemoveAt(i);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ carta no deck", GUILayout.Width(150f)))
                {
                    var nextCardId = cardIds.Find(id => !ship.deck.Exists(entry => entry.cardId == id));
                    if (!string.IsNullOrEmpty(nextCardId))
                    {
                        ship.deck.Add(new DeckEntry { cardId = nextCardId });
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private List<string> CardIds()
        {
            var ids = new List<string>();
            for (var i = 0; i < config.cards.Count; i++)
            {
                ids.Add(config.cards[i].id);
            }
            return ids;
        }

        // ------------------------------------------------------------------
        // Enemies
        // ------------------------------------------------------------------

        private void DrawEnemies()
        {
            foreach (var enemy in config.enemies)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(enemy.name + "   (" + enemy.kind + ")", EditorStyles.boldLabel);

                enemy.kind = (EnemyKind)EditorGUILayout.EnumPopup("Tipo", enemy.kind);
                enemy.name = EditorGUILayout.TextField("Nome", enemy.name);
                enemy.maxHp = EditorGUILayout.FloatField("HP", enemy.maxHp);
                enemy.collisionDamage = EditorGUILayout.FloatField("Dano de colisão", enemy.collisionDamage);
                enemy.speed = EditorGUILayout.FloatField("Velocidade", enemy.speed);
                enemy.radius = EditorGUILayout.FloatField("Raio", enemy.radius);
                enemy.behaviour = (MoveBehaviour)EditorGUILayout.EnumPopup("Movimento", enemy.behaviour);
                enemy.tint = EditorGUILayout.TextField("Cor (RRGGBB)", enemy.tint);
                enemy.shapeSides = EditorGUILayout.IntField("Lados do polígono", enemy.shapeSides);
                enemy.fragmentInto = (EnemyKind)EditorGUILayout.EnumPopup("Fragmenta em", enemy.fragmentInto);
                enemy.fragmentCount = EditorGUILayout.IntField("Qtd. de fragmentos", enemy.fragmentCount);

                if (enemy.behaviour == MoveBehaviour.Core)
                {
                    enemy.summonInterval = EditorGUILayout.FloatField("Intervalo de reforços (s)",
                        enemy.summonInterval);
                    enemy.summonCount = EditorGUILayout.IntField("Reforços por convocação", enemy.summonCount);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        // ------------------------------------------------------------------
        // Waves
        // ------------------------------------------------------------------

        private void DrawWaves()
        {
            var kinds = System.Enum.GetNames(typeof(EnemyKind));

            foreach (var wave in config.waves)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                var expanded = expandedWaves.Contains(wave.index);
                if (GUILayout.Button(expanded ? "▼" : "▶", GUILayout.Width(26f)))
                {
                    if (expanded) expandedWaves.Remove(wave.index);
                    else expandedWaves.Add(wave.index);
                }

                EditorGUILayout.LabelField("Onda " + wave.index, EditorStyles.boldLabel, GUILayout.Width(80f));
                EditorGUILayout.LabelField(wave.label, GUILayout.Width(300f));
                EditorGUILayout.LabelField(wave.TotalEnemies() + " inimigos", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                if (expandedWaves.Contains(wave.index))
                {
                    EditorGUI.indentLevel++;
                    wave.label = EditorGUILayout.TextField("Composição", wave.label);
                    wave.designNote = EditorGUILayout.TextField("Nota de design", wave.designNote);
                    wave.spawnInterval = EditorGUILayout.FloatField("Intervalo de spawn (s)",
                        wave.spawnInterval);
                    wave.isBossWave = EditorGUILayout.Toggle("Wave de chefe", wave.isBossWave);

                    for (var i = 0; i < wave.entries.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        var index = System.Array.IndexOf(kinds, wave.entries[i].kind.ToString());
                        var picked = EditorGUILayout.Popup(Mathf.Max(0, index), kinds, GUILayout.Width(160f));
                        wave.entries[i].kind = (EnemyKind)System.Enum.Parse(typeof(EnemyKind), kinds[picked]);
                        wave.entries[i].count = EditorGUILayout.IntField(wave.entries[i].count,
                            GUILayout.Width(50f));
                        if (GUILayout.Button("×", GUILayout.Width(24f)))
                        {
                            wave.entries.RemoveAt(i);
                            EditorGUILayout.EndHorizontal();
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                            return;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+ grupo de inimigos", GUILayout.Width(180f)))
                    {
                        wave.entries.Add(new WaveEntry { kind = EnemyKind.MeteorSmall, count = 1 });
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        private void DrawValidation()
        {
            if (GUILayout.Button("Validar conteúdo", GUILayout.Width(160f)))
            {
                validation = config.Validate();
            }

            EditorGUILayout.Space();

            if (validation == null)
            {
                EditorGUILayout.HelpBox("Clique em validar para checar decks, referências e ondas.",
                    MessageType.Info);
                return;
            }

            if (validation.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum problema encontrado.", MessageType.Info);
                return;
            }

            foreach (var problem in validation)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
        }
    }
}
