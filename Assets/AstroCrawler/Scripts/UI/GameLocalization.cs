using System.Collections.Generic;
using UnityEngine;

namespace AstroCrawler
{
    public static class GameLocalization
    {
        // The original numeric values are part of the persisted player preference.
        public enum Language { Portuguese, English, Spanish, Japanese, Hindi, Korean, SimplifiedChinese, German, French, Italian }
        public const int LanguageCount = 10;
        private static readonly string[] codes = { "pt", "en", "es", "ja", "hi", "ko", "zh-Hans", "de", "fr", "it" };
        private static readonly string[] names = { "Português", "English", "Español", "日本語", "हिन्दी", "한국어", "简体中文", "Deutsch", "Français", "Italiano" };
        public static string Code(Language language) => codes[Valid(language) ? (int)language : 0];
        public static string NativeName(Language language) => names[Valid(language) ? (int)language : 0];
        private static bool Valid(Language language) => (int)language >= 0 && (int)language < LanguageCount;

        private const string LanguageKey = "AstroCrawler.Language";
        private static Language? current;
        private static readonly Dictionary<string, string[]> content = new Dictionary<string, string[]>();

        public static Language Current
        {
            get
            {
                if (!current.HasValue)
                {
                    var saved = (Language)PlayerPrefs.GetInt(LanguageKey, 0);
                    current = Valid(saved) ? saved : Language.Portuguese;
                }
                return current.Value;
            }
        }

        public static void Set(Language language)
        {
            if (!Valid(language)) language = Language.Portuguese;
            current = language;
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            PlayerPrefs.Save();
        }

        public static void ResetSession()
        {
            current = null;
            loadedLanguage = null;
            additional.Clear();
        }

        public static string Choose(string portuguese, string english, string spanish)
        {
            switch (Current)
            {
                case Language.English: return english;
                case Language.Spanish: return spanish;
                case Language.Portuguese: return portuguese;
                default: return Additional(portuguese, english);
            }
        }

        public static string Content(string portuguese)
        {
            if (Current == Language.Portuguese || string.IsNullOrEmpty(portuguese)) return portuguese;
            string[] translated;
            var known = content.TryGetValue(portuguese, out translated);
            if (Current == Language.English) return known ? translated[0] : portuguese;
            if (Current == Language.Spanish) return known ? translated[1] : portuguese;
            return Additional(portuguese, known ? translated[0] : portuguese);
        }

        [System.Serializable] private sealed class LocaleEntry { public string key; public string value; }
        [System.Serializable] private sealed class LocaleTable { public LocaleEntry[] entries; }
        private static Language? loadedLanguage;
        private static readonly Dictionary<string, string> additional = new Dictionary<string, string>();

        private static string Additional(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (loadedLanguage != Current)
            {
                additional.Clear();
                loadedLanguage = Current;
                var resource = Resources.Load<TextAsset>("AstroCrawler/Localization/" + Code(Current));
                var table = resource != null ? JsonUtility.FromJson<LocaleTable>(resource.text) : null;
                if (table != null && table.entries != null)
                {
                    foreach (var entry in table.entries)
                        if (!string.IsNullOrEmpty(entry.key)) additional[entry.key] = entry.value;
                }
                else Debug.LogError("[AstroCrawler] Missing locale: " + Code(Current));
            }
            return additional.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static void Add(string portuguese, string english, string spanish)
        {
            content[portuguese] = new[] { english, spanish };
        }

        static GameLocalization()
        {
            Add("Melhorias e correções nesta versão.", "Improvements and fixes in this version.", "Mejoras y correcciones en esta versión.");
            // Release notes 0.1.0 (launch).
            Add("Bem-vindo ao Astro Blitz! Obrigado por jogar desde o lançamento.",
                "Welcome to Astro Blitz! Thank you for playing since launch.",
                "¡Bienvenido a Astro Blitz! Gracias por jugar desde el lanzamiento.");
            Add("Pilote naves únicas, escolha melhorias a cada nível e vença o Núcleo Aberrante nas dez fases.",
                "Pilot unique ships, pick upgrades at every level and beat the Aberrant Core across ten stages.",
                "Pilota naves únicas, elige mejoras en cada nivel y vence al Núcleo Aberrante en las diez fases.");
            Add("Esta é só a primeira versão: teremos atualizações toda semana para melhorar o jogo.",
                "This is only the first version: we will release updates every week to improve the game.",
                "Esta es solo la primera versión: habrá actualizaciones cada semana para mejorar el juego.");
            Add("Dúvidas, ideias ou bugs? Fale com a equipe pelo botão abaixo.",
                "Questions, ideas or bugs? Contact the team with the button below.",
                "¿Dudas, ideas o errores? Habla con el equipo con el botón de abajo.");
            Add("Novidade: Noel Polar, a nave de Natal com listras vermelhas e brancas.", "New: Polar Noel, the Christmas ship with red and white stripes.", "Novedad: Noel Polar, la nave navideña con rayas rojas y blancas.");
            Add("Seis cartas exclusivas: bolas de neve, círculo de gelo, presentes explosivos, estilhaços e nevasca.", "Six exclusive cards: snowballs, frost circle, explosive gifts, ice shards and blizzard.", "Seis cartas exclusivas: bolas de nieve, círculo de hielo, regalos explosivos, fragmentos y ventisca.");
            Add("Estrela Natalina: inimigos marcados recebem 35% mais dano durante 6 segundos.", "Christmas Star: marked enemies take 35% more damage for 6 seconds.", "Estrella Navideña: los enemigos marcados reciben un 35% más de daño durante 6 segundos.");
            Add("Novo canal de suporte para dúvidas, sugestões e relatos de bugs.", "New support channel for questions, suggestions and bug reports.", "Nuevo canal de soporte para dudas, sugerencias e informes de errores.");

            Add("Bola de Neve Ricocheteante", "Bouncing Snowball", "Bola de Nieve Rebotante");
            Add("A cada 12s, uma bola rebate nas bordas da tela por 6s e causa 18 de dano por contato.", "Every 12s, a snowball bounces off screen edges for 6s, dealing 18 contact damage.", "Cada 12s, una bola rebota en los bordes durante 6s e inflige 18 de daño por contacto.");
            Add("A cada 12s, uma bola rebate nas bordas da tela por 8s e causa 26 de dano por contato.", "Every 12s, a snowball bounces off screen edges for 8s, dealing 26 contact damage.", "Cada 12s, una bola rebota en los bordes durante 8s e inflige 26 de daño por contacto.");
            Add("A cada 12s, uma bola rebate nas bordas da tela por 10s e causa 36 de dano por contato.", "Every 12s, a snowball bounces off screen edges for 10s, dealing 36 contact damage.", "Cada 12s, una bola rebota en los bordes durante 10s e inflige 36 de daño por contacto.");
            Add("Bola de Neve Gigante", "Giant Snowball", "Bola de Nieve Gigante");
            Add("A cada 16s, dispara uma bola gigante que atravessa inimigos por 6s: 65 de dano e 45% de lentidão.", "Every 16s, launch a giant piercing snowball for 6s: 65 damage and 45% slow.", "Cada 16s, lanza una bola gigante que atraviesa enemigos durante 6s: 65 de daño y 45% de ralentización.");
            Add("A cada 15s, dispara uma bola gigante que atravessa inimigos por 6s: 100 de dano e 45% de lentidão.", "Every 15s, launch a giant piercing snowball for 6s: 100 damage and 45% slow.", "Cada 15s, lanza una bola gigante que atraviesa enemigos durante 6s: 100 de daño y 45% de ralentización.");
            Add("A cada 14s, dispara uma bola gigante que atravessa inimigos por 6s: 145 de dano e 45% de lentidão.", "Every 14s, launch a giant piercing snowball for 6s: 145 damage and 45% slow.", "Cada 14s, lanza una bola gigante que atraviesa enemigos durante 6s: 145 de daño y 45% de ralentización.");
            Add("Círculo de Gelo", "Frost Circle", "Círculo de Hielo");
            Add("A cada 14s, cria gelo ao redor da nave por 5s: 8 de dano a cada 0,5s e 35% de lentidão.", "Every 14s, create frost around the ship for 5s: 8 damage every 0.5s and 35% slow.", "Cada 14s, crea hielo alrededor de la nave durante 5s: 8 de daño cada 0,5s y 35% de ralentización.");
            Add("A cada 13s, cria gelo ao redor da nave por 6s: 12 de dano a cada 0,5s e 45% de lentidão.", "Every 13s, create frost around the ship for 6s: 12 damage every 0.5s and 45% slow.", "Cada 13s, crea hielo alrededor de la nave durante 6s: 12 de daño cada 0,5s y 45% de ralentización.");
            Add("A cada 12s, cria gelo ao redor da nave por 7s: 16 de dano a cada 0,5s e 55% de lentidão.", "Every 12s, create frost around the ship for 7s: 16 damage every 0.5s and 55% slow.", "Cada 12s, crea hielo alrededor de la nave durante 7s: 16 de daño cada 0,5s y 55% de ralentización.");
            Add("Presentes Explosivos", "Explosive Gifts", "Regalos Explosivos");
            Add("A cada 11s, deixa um presente na posição de um inimigo visível. Explode após 1s, causando 70 de dano em área.", "Every 11s, place a gift at a visible enemy position. It explodes after 1s for 70 area damage.", "Cada 11s, deja un regalo donde está un enemigo visible. Explota tras 1s e inflige 70 de daño en área.");
            Add("A cada 10s, deixa um presente na posição de um inimigo visível. Explode após 1s, causando 105 de dano em área.", "Every 10s, place a gift at a visible enemy position. It explodes after 1s for 105 area damage.", "Cada 10s, deja un regalo donde está un enemigo visible. Explota tras 1s e inflige 105 de daño en área.");
            Add("A cada 9s, deixa um presente na posição de um inimigo visível. Explode após 1s, causando 150 de dano em área.", "Every 9s, place a gift at a visible enemy position. It explodes after 1s for 150 area damage.", "Cada 9s, deja un regalo donde está un enemigo visible. Explota tras 1s e inflige 150 de daño en área.");
            Add("Estilhaços de Gelo", "Ice Shards", "Fragmentos de Hielo");
            Add("A cada 9s, dispara 6 cristais em todas as direções: 20 de dano perfurante e 15% de lentidão.", "Every 9s, fire 6 crystals in all directions: 20 piercing damage and 15% slow.", "Cada 9s, dispara 6 cristales en todas direcciones: 20 de daño perforante y 15% de ralentización.");
            Add("A cada 8s, dispara 8 cristais em todas as direções: 30 de dano perfurante e 20% de lentidão.", "Every 8s, fire 8 crystals in all directions: 30 piercing damage and 20% slow.", "Cada 8s, dispara 8 cristales en todas direcciones: 30 de daño perforante y 20% de ralentización.");
            Add("A cada 7s, dispara 10 cristais em todas as direções: 42 de dano perfurante e 25% de lentidão.", "Every 7s, fire 10 crystals in all directions: 42 piercing damage and 25% slow.", "Cada 7s, dispara 10 cristales en todas direcciones: 42 de daño perforante y 25% de ralentización.");
            Add("Nevasca Localizada", "Localized Blizzard", "Ventisca Localizada");
            Add("A cada 18s, cria até 2 zonas de nevasca sobre inimigos por 4s: 10 de dano a cada 0,5s e 30% de lentidão.", "Every 18s, create up to 2 blizzard zones on enemies for 4s: 10 damage every 0.5s and 30% slow.", "Cada 18s, crea hasta 2 zonas de ventisca sobre enemigos durante 4s: 10 de daño cada 0,5s y 30% de ralentización.");
            Add("A cada 17s, cria até 3 zonas de nevasca sobre inimigos por 5s: 15 de dano a cada 0,5s e 35% de lentidão.", "Every 17s, create up to 3 blizzard zones on enemies for 5s: 15 damage every 0.5s and 35% slow.", "Cada 17s, crea hasta 3 zonas de ventisca sobre enemigos durante 5s: 15 de daño cada 0,5s y 35% de ralentización.");
            Add("A cada 16s, cria até 4 zonas de nevasca sobre inimigos por 6s: 21 de dano a cada 0,5s e 40% de lentidão.", "Every 16s, create up to 4 blizzard zones on enemies for 6s: 21 damage every 0.5s and 40% slow.", "Cada 16s, crea hasta 4 zonas de ventisca sobre enemigos durante 6s: 21 de daño cada 0,5s y 40% de ralentización.");
            Add("Pinheiro Natalino", "Christmas Pine", "Pino Navideño");
            Add("Cristal Polar", "Polar Crystal", "Cristal Polar");
            Add("Noel Polar", "Polar Noel", "Noel Polar");
            Add("Listras de bengala doce e um arsenal de inverno.", "Candy-cane stripes and a winter arsenal.", "Rayas de bastón de caramelo y un arsenal invernal.");
            Add("Natal: bolas de neve, gelo e presentes explosivos.", "Christmas: snowballs, frost and explosive gifts.", "Navidad: bolas de nieve, hielo y regalos explosivos.");
            Add("Estrela Natalina", "Christmas Star", "Estrella Navideña");
            Add("Marca os inimigos no alcance: recebem 35% mais dano por 6s.", "Mark enemies in range: they take 35% more damage for 6s.", "Marca a los enemigos al alcance: reciben un 35% más de daño durante 6s.");

            // Stable effect names shared by all three card levels.
            Add("Tiros Extras", "Extra Shots", "Disparos Extra");
            Add("Escudo Recarregável", "Recharging Shield", "Escudo Recargable");
            Add("Mísseis Teleguiados", "Homing Missiles", "Misiles Teledirigidos");
            Add("Tiros Perfurantes", "Piercing Shots", "Disparos Perforantes");
            Add("Cadência Aumentada", "Faster Fire Rate", "Mayor Cadencia");
            Add("Mísseis ao Curar", "Missiles on Healing", "Misiles al Curarse");
            Add("Chance Crítica", "Critical Chance", "Probabilidad Crítica");
            Add("Cura por Abate", "Healing on Kill", "Curación por Baja");
            Add("Repulsão Periódica", "Periodic Knockback", "Repulsión Periódica");
            Add("Cura Periódica", "Periodic Healing", "Curación Periódica");
            Add("Barreira de Destroços", "Debris Barrier", "Barrera de Escombros");
            Add("Tiro Explosivo", "Explosive Shot", "Disparo Explosivo");
            Add("A cada 6 tiros do canhão, o último é crítico garantido.", "Every 6 cannon shots, the last is a guaranteed critical.", "Cada 6 disparos del cañón, el último es un crítico garantizado.");
            Add("A cada 5 tiros do canhão, o último é crítico garantido.", "Every 5 cannon shots, the last is a guaranteed critical.", "Cada 5 disparos del cañón, el último es un crítico garantizado.");
            Add("A cada 4 tiros do canhão, o último é crítico garantido.", "Every 4 cannon shots, the last is a guaranteed critical.", "Cada 4 disparos del cañón, el último es un crítico garantizado.");
            Add("Crítico Garantido", "Guaranteed Critical", "Crítico Garantizado");
            Add("Lentidão Global", "Global Slow", "Ralentización Global");
            Add("Paralisia por Vida Baixa", "Low-Health Paralysis", "Parálisis por Vida Baja");
            Add("Raios em Cadeia", "Chain Lightning", "Rayos en Cadena");
            Add("Drones Atiradores", "Shooter Drones", "Drones Tiradores");
            Add("Sobrecarga Mais Rápida", "Faster Overdrive Recharge", "Recarga de Sobrecarga Rápida");
            Add("Redução de Dano", "Damage Reduction", "Reducción de Daño");
            Add("Dano contra Chefes", "Boss Damage", "Daño contra Jefes");
            Add("Estilhaços por Abate", "Shrapnel on Kill", "Metralla por Baja");
            Add("Tiros Diagonais", "Diagonal Shots", "Disparos Diagonales");
            Add("Aura de Dano", "Damage Aura", "Aura de Daño");
            Add("Explosão Periódica", "Periodic Blast", "Explosión Periódica");
            Add("Bombardeio por Satélite", "Satellite Bombardment", "Bombardeo por Satélite");
            Add("Bônus de XP", "XP Bonus", "Bonificación de XP");
            Add("Regeneração de Vida", "Health Regeneration", "Regeneración de Vida");
            Add("Ao receber dano fatal, revive com 30% do HP e 2s de imunidade. Recarga: 60s de combate.", "On lethal damage, revive with 30% HP and 2s immunity. Cooldown: 60s of combat.", "Al recibir daño letal, revive con 30% de vida y 2s de inmunidad. Recarga: 60s de combate.");
            Add("Ao receber dano fatal, revive com 45% do HP, 2s de imunidade e empurra inimigos. Recarga: 60s de combate.", "On lethal damage, revive with 45% HP, 2s immunity and push enemies away. Cooldown: 60s of combat.", "Al recibir daño letal, revive con 45% de vida, 2s de inmunidad y empuja enemigos. Recarga: 60s de combate.");
            Add("Ao receber dano fatal, revive com 60% do HP, 2s de imunidade, empurra e causa 60 de dano ao redor. Recarga: 60s de combate.", "On lethal damage, revive with 60% HP, 2s immunity, push enemies and deal 60 area damage. Cooldown: 60s of combat.", "Al recibir daño letal, revive con 60% de vida, 2s de inmunidad, empuja y causa 60 de daño en área. Recarga: 60s de combate.");
            Add("Reviver", "Revive", "Revivir");
            Add("Cadência por Abate", "Fire Rate on Kill", "Cadencia por Baja");
            Add("Execução por Vida Baixa", "Low-Health Execution", "Ejecución por Vida Baja");
            Add("Dano na Sobrecarga", "Overdrive Damage", "Daño en Sobrecarga");
            Add("Espinhos ao Colidir", "Collision Thorns", "Espinas al Chocar");
            Add("Égide Duradoura", "Longer Aegis", "Égida Duradera");
            Add("Cura ao Bloquear", "Healing on Block", "Curación al Bloquear");
            Add("Fenda de Lentidão", "Slowing Rift", "Grieta Ralentizadora");
            Add("Recarga da Estase por Abate", "Stasis Recharge on Kill", "Recarga de Estasis por Baja");
            Add("Repetição de Acertos", "Repeating Hits", "Repetición de Impactos");
            Add("Escudo por Excesso de Cura", "Overheal Shield", "Escudo por Sobrecuración");
            Add("Dano ao Curar", "Damage on Healing", "Daño al Curarse");
            Add("Cura de Emergência", "Emergency Healing", "Curación de Emergencia");
            Add("Feixe Perfurante", "Piercing Beam", "Rayo Perforante");
            Add("Alvo Vulnerável", "Vulnerable Target", "Objetivo Vulnerable");
            Add("Explosão na Amplificação", "Amplify Blast", "Explosión al Amplificar");

            // Deck identities and special recharge cards.
            Add("Recarga Ofensiva", "Offensive Recharge", "Recarga Ofensiva");
            Add("Reduz em 15% a recarga da habilidade Sobrecarga ofensiva.", "Reduces Offensive overdrive ability cooldown by 15%.", "Reduce un 15% la recarga de la habilidad Sobrecarga ofensiva.");
            Add("Recarga Ofensiva Avançada", "Advanced Offensive Recharge", "Recarga Ofensiva Avanzada");
            Add("Reduz em 25% a recarga da habilidade Sobrecarga ofensiva.", "Reduces Offensive overdrive ability cooldown by 25%.", "Reduce un 25% la recarga de la habilidad Sobrecarga ofensiva.");
            Add("Recarga Ofensiva Máxima", "Maximum Offensive Recharge", "Recarga Ofensiva Máxima");
            Add("Reduz em 35% a recarga da habilidade Sobrecarga ofensiva.", "Reduces Offensive overdrive ability cooldown by 35%.", "Reduce un 35% la recarga de la habilidad Sobrecarga ofensiva.");
            Add("Ofensiva: cura por abate, crítico e recarga da Sobrecarga ofensiva.", "Offense: healing on kills, critical hits and Offensive overdrive recharge.", "Ofensiva: curación por bajas, críticos y recarga de Sobrecarga ofensiva.");
            Add("Suporte: autorreparo, mísseis por cura e drones de apoio.", "Support: self-repair, missiles on healing and support drones.", "Apoyo: autorreparación, misiles al curarse y drones de apoyo.");

            // Unique armor and boss-hunter cards.
            Add("Blindagem Reforçada", "Reinforced Armor", "Blindaje Reforzado");
            Add("Reduz em 10% todo o dano recebido durante a run.", "Reduces all incoming damage by 10% during the run.", "Reduce un 10% todo el daño recibido durante la partida.");
            Add("Blindagem Avançada", "Advanced Armor", "Blindaje Avanzado");
            Add("Reduz em 18% todo o dano recebido durante a run.", "Reduces all incoming damage by 18% during the run.", "Reduce un 18% todo el daño recibido durante la partida.");
            Add("Blindagem Inabalável", "Unbreakable Armor", "Blindaje Inquebrantable");
            Add("Reduz em 25% todo o dano recebido durante a run.", "Reduces all incoming damage by 25% during the run.", "Reduce un 25% todo el daño recibido durante la partida.");
            Add("Caça ao Chefe", "Boss Hunter", "Cazador de Jefes");
            Add("Disparos do canhão mirados no chefe causam +25% de dano.", "Cannon shots aimed at the boss deal +25% damage.", "Los disparos del cañón dirigidos al jefe causan +25% de daño.");
            Add("Predador de Chefes", "Boss Predator", "Depredador de Jefes");
            Add("Disparos do canhão mirados no chefe causam +40% de dano.", "Cannon shots aimed at the boss deal +40% damage.", "Los disparos del cañón dirigidos al jefe causan +40% de daño.");
            Add("Flagelo dos Chefes", "Boss Scourge", "Azote de Jefes");
            Add("Disparos do canhão mirados no chefe causam +60% de dano.", "Cannon shots aimed at the boss deal +60% damage.", "Los disparos del cañón dirigidos al jefe causan +60% de daño.");
            Add("Fortificação: repulsão, destroços e blindagem contra dano.", "Fortification: repulsion, debris and damage-reducing armor.", "Fortificación: repulsión, escombros y blindaje contra daño.");

            // Periodic guaranteed critical replaces gravity wells.
            Add("Pulso Crítico", "Critical Pulse", "Pulso Crítico");
            Add("Após 10s de combate, o próximo tiro é crítico garantido. Reinicia após o disparo; não acumula cargas.", "After 10s of combat, the next shot is a guaranteed critical. Restarts after firing; no stored stacks.", "Tras 10s de combate, el próximo disparo es crítico garantizado. Reinicia al disparar; no acumula cargas.");
            Add("Ritmo Crítico", "Critical Rhythm", "Ritmo Crítico");
            Add("Após 7s de combate, o próximo tiro é crítico garantido. Reinicia após o disparo; não acumula cargas.", "After 7s of combat, the next shot is a guaranteed critical. Restarts after firing; no stored stacks.", "Tras 7s de combate, el próximo disparo es crítico garantizado. Reinicia al disparar; no acumula cargas.");
            Add("Ressonância Crítica", "Critical Resonance", "Resonancia Crítica");
            Add("Após 5s de combate, o próximo tiro é crítico garantido. Reinicia após o disparo; não acumula cargas.", "After 5s of combat, the next shot is a guaranteed critical. Restarts after firing; no stored stacks.", "Tras 5s de combate, el próximo disparo es crítico garantizado. Reinicia al disparar; no acumula cargas.");
            Add("Aumenta permanentemente a cadência de tiro em +60%.", "Permanently increases fire rate by +60%.", "Aumenta permanentemente la cadencia de tiro un +60%.");
            Add("Aumenta permanentemente a cadência de tiro em +85%.", "Permanently increases fire rate by +85%.", "Aumenta permanentemente la cadencia de tiro un +85%.");
            Add("Controle e precisão: crítico garantido, lentidão e paralisia temporal.", "Control and precision: guaranteed critical hits, slow and temporal paralysis.", "Control y precisión: críticos garantizados, ralentización y parálisis temporal.");

            // All critical hits use the same base multiplier.
            Add("Permanentemente, 12% de chance de crítico. Multiplicador x2 + bônus permanentes.", "Permanently, 12% critical chance. Multiplier x2 + permanent bonuses.", "Permanentemente, 12% de probabilidad de crítico. Multiplicador x2 + bonos permanentes.");
            Add("Permanentemente, 25% de chance de crítico. Multiplicador x2 + bônus permanentes.", "Permanently, 25% critical chance. Multiplier x2 + permanent bonuses.", "Permanentemente, 25% de probabilidad de crítico. Multiplicador x2 + bonos permanentes.");
            Add("Permanentemente, 32% de chance de crítico. Multiplicador x2 + bônus permanentes.", "Permanently, 32% critical chance. Multiplier x2 + permanent bonuses.", "Permanentemente, 32% de probabilidad de crítico. Multiplicador x2 + bonos permanentes.");

            // Orion artillery upgrades.
            Add("Arco Voltaico", "Voltaic Arc", "Arco Voltaico");
            Add("Acertos do canhão geram raios em até 2 outros inimigos próximos: 12 de dano por alvo. Intervalo de 3s.", "Cannon hits arc to up to 2 other nearby enemies: 12 damage each. 3s interval.", "Los impactos del cañón lanzan rayos a otros 2 enemigos cercanos: 12 de daño por objetivo. Intervalo de 3s.");
            Add("Corrente Voltaica", "Voltaic Chain", "Corriente Voltaica");
            Add("Acertos do canhão geram raios em até 3 outros inimigos próximos: 18 de dano por alvo. Intervalo de 2,5s.", "Cannon hits arc to up to 3 other nearby enemies: 18 damage each. 2.5s interval.", "Los impactos del cañón lanzan rayos a otros 3 enemigos cercanos: 18 de daño por objetivo. Intervalo de 2,5s.");
            Add("Tempestade Voltaica", "Voltaic Storm", "Tormenta Voltaica");
            Add("Acertos do canhão geram raios em até 4 outros inimigos próximos: 25 de dano por alvo. Intervalo de 2s.", "Cannon hits arc to up to 4 other nearby enemies: 25 damage each. 2s interval.", "Los impactos del cañón lanzan rayos a otros 4 enemigos cercanos: 25 de daño por objetivo. Intervalo de 2s.");
            Add("Disparo de Cerco", "Siege Shot", "Disparo de Asedio");
            Add("Após 8s de combate, carrega um tiro com explosão de 40 de dano em área. Não acumula cargas.", "After 8s of combat, charges one shot with a 40-damage area explosion. Does not stack charges.", "Tras 8s de combate, carga un disparo con explosión de 40 de daño en área. No acumula cargas.");
            Add("Artilharia de Cerco", "Siege Artillery", "Artillería de Asedio");
            Add("Após 6s de combate, carrega um tiro com explosão de 65 de dano em área. Não acumula cargas.", "After 6s of combat, charges one shot with a 65-damage area explosion. Does not stack charges.", "Tras 6s de combate, carga un disparo con explosión de 65 de daño en área. No acumula cargas.");
            Add("Devastação de Cerco", "Siege Devastation", "Devastación de Asedio");
            Add("Após 4s de combate, carrega um tiro com explosão de 90 de dano em área. Não acumula cargas.", "After 4s of combat, charges one shot with a 90-damage area explosion. Does not stack charges.", "Tras 4s de combate, carga un disparo con explosión de 90 de daño en área. No acumula cargas.");
            Add("Artilharia: raios em cadeia, disparos de cerco e dano extra contra chefes.", "Artillery: chain lightning, siege shots and bonus damage against bosses.", "Artillería: rayos en cadena, disparos de asedio y daño adicional contra jefes.");

            // Ships
            Add("Aurora Médica", "Medical Aurora", "Aurora Médica");
            Add("Orion", "Orion", "Orion");
            Add("Uma nave de suporte que reconstrói o próprio casco.", "A support ship that rebuilds its own hull.", "Una nave de apoyo que reconstruye su propio casco.");
            Add("Sustentação: reparo e defesa mantêm a nave no combate.", "Sustain: repair and defense keep the ship in combat.", "Supervivencia: reparación y defensa mantienen la nave en combate.");
            Add("Canhões pesados: cada disparo precisa contar.", "Heavy cannons: every shot must count.", "Cañones pesados: cada disparo debe contar.");
            Add("Artilharia pesada: dano e explosões compensam a cadência baixa.", "Heavy artillery: damage and explosions offset the low fire rate.", "Artillería pesada: daño y explosiones compensan la baja cadencia.");
            Add("Vanguarda-7", "Vanguard-7", "Vanguardia-7");
            Add("Feita para acabar com a briga rápido, não para durar muito tempo nela.", "Built to end the fight quickly, not to survive a long one.", "Hecha para terminar la pelea rápido, no para resistirla mucho tiempo.");
            Add("Ofensiva pura: deck inclinado para Ataque Duplo e Tiro Penetrante, reforçado por cartas de alto risco e alto retorno que recompensam jogar agressivo.", "Pure offense: a deck built around Double Attack and Piercing Shot, backed by high-risk cards that reward aggressive play.", "Ofensiva pura: un mazo centrado en Ataque Doble y Disparo Perforante, con cartas arriesgadas que recompensan el juego agresivo.");
            Add("Bastião Aegis", "Aegis Bastion", "Bastión Aegis");
            Add("Não é a mais rápida a atirar, mas é a última a cair.", "It is not the fastest shot, but it is the last to fall.", "No es la más rápida al disparar, pero sí la última en caer.");
            Add("Tanque de longa duração: deck inclinado para Defesa e Buraco Negro, reforçado por cartas de controle e sustentação.", "A durable tank: a deck built around Shield and Black Hole, with control and sustain cards.", "Un tanque resistente: un mazo centrado en Defensa y Agujero Negro, con cartas de control y supervivencia.");
            Add("Espectro Nulo", "Null Specter", "Espectro Nulo");
            Add("Protótipo experimental que dobra o espaço ao seu redor — poderoso, mas com casco de papel.", "An experimental prototype that bends space around it: powerful, but fragile.", "Un prototipo experimental que dobla el espacio a su alrededor: poderoso, pero frágil.");
            Add("Builds de área e sinergia: deck inclinado para Mísseis e Buraco Negro, reforçado por cartas gravitacionais que aprofundam o combo de agrupar e detonar.", "Area damage and synergy: a deck built around Missiles and Black Hole, with gravity cards that pull enemies together before detonation.", "Daño de área y sinergia: un mazo centrado en Misiles y Agujero Negro, con cartas gravitacionales que agrupan enemigos antes de detonarlos.");

            // Cards: names, descriptions, evolved names and descriptions.
            // Level 3 (maximum) names and descriptions.
            Add("Salva Quádrupla", "Quadruple Volley", "Salva Cuádruple");
            Add("Dispara 4 projéteis em paralelo, permanentemente.", "Permanently fires 4 projectiles side by side.", "Dispara 4 proyectiles en paralelo de forma permanente.");
            Add("Aegis Total", "Total Aegis", "Aegis Total");
            Add("Recarrega 3 bloqueios a cada 12s de combate. Duram até serem consumidos.", "Recharges 3 blocks every 12s of combat. They last until used.", "Recarga 3 bloqueos cada 12 s de combate. Duran hasta consumirse.");
            Add("Chuva de Mísseis", "Missile Rain", "Lluvia de Misiles");
            Add("Dispara 4 mísseis com dano em área ampliado a cada 5 segundos de combate, quando há inimigos no alcance.", "Fires 4 missiles with larger area damage every 5 seconds of combat, when enemies are in range.", "Dispara 4 misiles con mayor daño en área cada 5 segundos de combate, cuando hay enemigos en el alcance.");
            Add("Singularidade", "Singularity", "Singularidad");
            Add("Cria um buraco negro enorme de 10s que explode com força ao final, a cada 14s.", "Creates a huge 10-second black hole that detonates hard at the end, every 14s.", "Crea un agujero negro enorme de 10 s que explota con fuerza al final, cada 14 s.");
            Add("Lança Penetrante", "Piercing Lance", "Lanza Penetrante");
            Add("Penetração permanente e mais 25% de dano por tiro (total +45%).", "Permanent piercing and a further +25% damage per shot (+45% in total).", "Penetración permanente y un 25 % más de daño por disparo (+45 % en total).");
            Add("Overdrive Máximo", "Maximum Overdrive", "Sobremarcha Máxima");
            Add("Tempestade Restauradora", "Restorative Storm", "Tormenta Restauradora");
            Add("Sempre que recuperar vida, dispara 10 mísseis retos em todas as direções. Cada míssil causa 24 de dano. Não ativa com vida cheia.", "Whenever HP is restored, fires 10 straight missiles in all directions. Each deals 24 damage. Does not trigger at full HP.", "Al recuperar vida, dispara 10 misiles rectos en todas las direcciones. Cada uno causa 24 de daño. No se activa con vida completa.");
            Add("Tempestade Selvagem", "Wild Tempest", "Tempestad Salvaje");
            Add("Fúria Inesgotável", "Unending Fury", "Furia Inagotable");
            Add("Cada abate recupera 3% do HP máximo da nave.\n\nEfeito permanente", "Each kill restores 3% of the ship's maximum HP.\n\nPermanent effect", "Cada baja recupera el 3 % de los PS máximos de la nave.\n\nEfecto permanente");
            Add("Repulsão Total", "Total Repulsion", "Repulsión Total");
            Add("A cada 6s, empurra com raio 260 e aplica 50% de lentidão por 4s.", "Every 6s, pushes with radius 260 and applies 50% slow for 4 seconds.", "Cada 6 s, empuja con radio de 260 y aplica 50 % de ralentización durante 4 s.");
            Add("Autorreparo Total", "Total Self-Repair", "Autorreparación Total");
            Add("Restaura 40% a cada 15s e repõe até 2 bloqueios extras, se foram consumidos.", "Restores 40% every 15s and replaces up to 2 extra blocks, if they were used.", "Restaura el 40 % cada 15 s y repone hasta 2 bloqueos extra, si se consumieron.");
            Add("Muralha Inabalável", "Unbreakable Wall", "Muralla Inquebrantable");
            Add("Cria uma barreira de HP 45 e duração 12s, a cada 10s.", "Creates a barrier with 45 HP for 12 seconds, every 10s.", "Crea una barrera de 45 PS durante 12 s, cada 10 s.");
            Add("Estase Temporal", "Temporal Stasis", "Estasis Temporal");
            Add("Esquadrão de Elite", "Elite Squadron", "Escuadrón de Élite");
            Add("A cada 20s, quatro drones apoiam a nave por 18s e causam 8 de dano cada por tiro.", "Every 20s, four drones support the ship for 18s and deal 8 damage per shot.", "Cada 20 s, cuatro drones apoyan la nave durante 18 s e infligen 8 de daño por disparo.");
            Add("Ataque Duplo", "Double Attack", "Ataque Doble");
            Add("Dobra permanentemente o número de projéteis disparados pela nave.", "Permanently doubles the number of projectiles fired by the ship.", "Duplica permanentemente la cantidad de proyectiles disparados por la nave.");
            Add("Ataque Triplo", "Triple Attack", "Ataque Triple");
            Add("Triplica permanentemente os projéteis disparados pela nave.", "Permanently triples the projectiles fired by the ship.", "Triplica permanentemente los proyectiles disparados por la nave.");
            Add("Defesa", "Shield", "Defensa");
            Add("Recarrega 1 bloqueio a cada 15s de combate. Dura até ser consumido.", "Recharges 1 block every 15s of combat. It lasts until used.", "Recarga 1 bloqueo cada 15 s de combate. Dura hasta consumirse.");
            Add("Escudo Reforçado", "Reinforced Shield", "Escudo Reforzado");
            Add("Recarrega 2 bloqueios a cada 15s de combate. Duram até serem consumidos.", "Recharges 2 blocks every 15s of combat. They last until used.", "Recarga 2 bloqueos cada 15 s de combate. Duran hasta consumirse.");
            Add("Mísseis", "Missiles", "Misiles");
            Add("Dispara automaticamente 2 mísseis teleguiados a cada 6 segundos de combate, quando há inimigos no alcance.", "Automatically fires 2 homing missiles every 6 seconds of combat when enemies are in range.", "Dispara automáticamente 2 misiles guiados cada 6 segundos de combate cuando hay enemigos al alcance.");
            Add("Enxame de Mísseis", "Missile Swarm", "Enjambre de Misiles");
            Add("Dispara automaticamente 3 mísseis com dano em área a cada 6 segundos de combate, quando há inimigos no alcance.", "Automatically fires 3 area-damage missiles every 6 seconds of combat when enemies are in range.", "Dispara automáticamente 3 misiles con daño de área cada 6 segundos de combate cuando hay enemigos al alcance.");
            Add("Buraco Negro", "Black Hole", "Agujero Negro");
            Add("Cria um buraco negro de 6s sobre os inimigos no alcance, a cada 16s.", "Creates a 6-second black hole on enemies in range, every 16s.", "Crea un agujero negro de 6 s sobre los enemigos en el alcance, cada 16 s.");
            Add("Colapso Estelar", "Stellar Collapse", "Colapso Estelar");
            Add("Cria um buraco negro maior de 9s que explode ao final, a cada 16s.", "Creates a larger 9-second black hole that explodes at the end, every 16s.", "Crea un agujero negro mayor de 9 s que explota al final, cada 16 s.");
            Add("Tiro Penetrante", "Piercing Shot", "Disparo Perforante");
            Add("Permanentemente, os projéteis atravessam inimigos em vez de serem destruídos.", "Projectiles permanently pierce enemies instead of being destroyed.", "Los proyectiles atraviesan permanentemente a los enemigos en vez de destruirse.");
            Add("Perfuração Total", "Full Penetration", "Perforación Total");
            Add("Penetração permanente pelo restante da run e +20% de dano por tiro.", "Permanent piercing for the rest of the run and +20% damage per shot.", "Perforación permanente durante el resto de la partida y +20 % de daño por disparo.");
            Add("Sobrecarga de Motores", "Engine Overload", "Sobrecarga de Motores");
            Add("Aumenta permanentemente a cadência de tiro em +30%.", "Permanently increases fire rate by 30%.", "Aumenta permanentemente la cadencia de tiro un 30 %.");
            Add("Overdrive Total", "Total Overdrive", "Sobremarcha Total");
            Add("Salva Restauradora", "Restorative Volley", "Salva Restauradora");
            Add("Sempre que recuperar vida, dispara 6 mísseis retos em todas as direções. Cada míssil causa 12 de dano. Não ativa com vida cheia.", "Whenever HP is restored, fires 6 straight missiles in all directions. Each deals 12 damage. Does not trigger at full HP.", "Al recuperar vida, dispara 6 misiles rectos en todas las direcciones. Cada uno causa 12 de daño. No se activa con vida completa.");
            Add("Barragem Restauradora", "Restorative Barrage", "Andanada Restauradora");
            Add("Sempre que recuperar vida, dispara 8 mísseis retos em todas as direções. Cada míssil causa 18 de dano. Não ativa com vida cheia.", "Whenever HP is restored, fires 8 straight missiles in all directions. Each deals 18 damage. Does not trigger at full HP.", "Al recuperar vida, dispara 8 misiles rectos en todas las direcciones. Cada uno causa 18 de daño. No se activa con vida completa.");
            Add("Rajada Selvagem", "Wild Volley", "Ráfaga Salvaje");
            Add("Fogo Descontrolado", "Uncontrolled Fire", "Fuego Descontrolado");
            Add("Fúria de Batalha", "Battle Fury", "Furia de Batalla");
            Add("Cada abate recupera 1% do HP máximo da nave.\n\nEfeito permanente", "Each kill restores 1% of the ship's maximum HP.\n\nPermanent effect", "Cada baja restaura el 1 % de los PS máximos de la nave.\n\nEfecto permanente");
            Add("Fúria Implacável", "Relentless Fury", "Furia Implacable");
            Add("Cada abate recupera 2% do HP máximo da nave.\n\nEfeito permanente", "Each kill restores 2% of the ship's maximum HP.\n\nPermanent effect", "Cada baja restaura el 2 % de los PS máximos de la nave.\n\nEfecto permanente");
            Add("Campo de Repulsão", "Repulsion Field", "Campo de Repulsión");
            Add("Empurra os inimigos próximos a cada 10s, quando há inimigos no alcance.", "Pushes nearby enemies away every 10s, when enemies are in range.", "Aleja a los enemigos cercanos cada 10 s, cuando hay enemigos en el alcance.");
            Add("Repulsão Amplificada", "Amplified Repulsion", "Repulsión Amplificada");
            Add("A cada 8s, empurra com raio 180 e aplica 30% de lentidão por 3s.", "Every 8s, pushes with radius 180 and applies 30% slow for 3 seconds.", "Cada 8 s, empuja con radio de 180 y aplica 30 % de ralentización durante 3 s.");
            Add("Reparo de Emergência", "Emergency Repair", "Reparación de Emergencia");
            Add("Restaura 20% do HP máximo a cada 20s de combate.", "Restores 20% of maximum HP every 20s of combat.", "Restaura el 20 % de los PS máximos cada 20 s de combate.");
            Add("Autorreparo Avançado", "Advanced Self-Repair", "Autorreparación Avanzada");
            Add("Restaura 30% a cada 18s e repõe 1 bloqueio extra, se ele foi consumido.", "Restores 30% every 18s and replaces 1 extra block, if it was used.", "Restaura el 30 % cada 18 s y repone 1 bloqueo extra, si se consumió.");
            Add("Muralha de Destroços", "Debris Wall", "Muro de Escombros");
            Add("Cria uma barreira (HP 10) diante da ameaça mais próxima, a cada 12s.", "Creates a barrier (10 HP) in front of the nearest threat, every 12s.", "Crea una barrera (10 PS) frente a la amenaza más cercana, cada 12 s.");
            Add("Muralha Reforçada", "Reinforced Wall", "Muro Reforzado");
            Add("Cria uma barreira de HP 25 e duração 9s, a cada 12s.", "Creates a barrier with 25 HP for 9 seconds, every 12s.", "Crea una barrera de 25 PS durante 9 s, cada 12 s.");
            Add("Distorção Temporal", "Temporal Distortion", "Distorsión Temporal");
            Add("Reduz permanentemente a velocidade de todos os inimigos em 10%.", "Permanently reduces all enemies' speed by 10%.", "Reduce permanentemente la velocidad de todos los enemigos un 10 %.");
            Add("Paralisia Temporal", "Temporal Paralysis", "Parálisis Temporal");
            Add("Dilatação Temporal", "Temporal Dilation", "Dilatación Temporal");
            Add("Dilatação Absoluta", "Absolute Dilation", "Dilatación Absoluta");
            Add("Estase Absoluta", "Absolute Stasis", "Estasis Absoluta");
            Add("Reduz permanentemente a velocidade de todos os inimigos em 20%.", "Permanently reduces all enemy movement speed by 20%.", "Reduce permanentemente la velocidad de todos los enemigos un 20 %.");
            Add("Reduz permanentemente a velocidade de todos os inimigos em 30%.", "Permanently reduces all enemy movement speed by 30%.", "Reduce permanentemente la velocidad de todos los enemigos un 30 %.");
            Add("Efeito permanente: inimigos no alcance com até 5% de HP ficam paralisados.", "Permanent effect: enemies in range at 5% HP or less are paralyzed.", "Efecto permanente: los enemigos al alcance con un 5 % de PS o menos quedan paralizados.");
            Add("Efeito permanente: inimigos no alcance com até 10% de HP ficam paralisados.", "Permanent effect: enemies in range at 10% HP or less are paralyzed.", "Efecto permanente: los enemigos al alcance con un 10 % de PS o menos quedan paralizados.");
            Add("Efeito permanente: inimigos no alcance com até 20% de HP ficam paralisados.", "Permanent effect: enemies in range at 20% HP or less are paralyzed.", "Efecto permanente: los enemigos al alcance con un 20 % de PS o menos quedan paralizados.");
            Add("Drones de Apoio", "Support Drones", "Drones de Apoyo");
            Add("A cada 22s, dois drones orbitam a nave por 12s e disparam contra inimigos no alcance a cada 1,2s.", "Every 22s, two drones orbit the ship for 12s and fire at enemies in range every 1.2s.", "Cada 22 s, dos drones orbitan la nave durante 12 s y disparan a enemigos en el alcance cada 1,2 s.");
            Add("Esquadrão de Apoio", "Support Squadron", "Escuadrón de Apoyo");
            Add("A cada 22s, três drones apoiam a nave por 15s e causam 7 de dano cada por tiro.", "Every 22s, three drones support the ship for 15s and deal 7 damage per shot.", "Cada 22 s, tres drones apoyan la nave durante 15 s e infligen 7 de daño por disparo.");

            // Base deck expansion and the level-unlocked exclusive cards.
            Add("Lâminas Orbitais", "Orbiting Blades", "Cuchillas Orbitales");
            Add("Duas lâminas orbitam a nave e cortam os inimigos que encostam: 10 de dano por corte.", "Two blades orbit the ship and slice the enemies they touch: 10 damage per cut.", "Dos hojas orbitan la nave y cortan a los enemigos que tocan: 10 de daño por corte.");
            Add("Anel de Lâminas", "Blade Ring", "Anillo de Hojas");
            Add("Três lâminas orbitam a nave: 14 de dano por corte.", "Three blades orbit the ship: 14 damage per cut.", "Tres hojas orbitan la nave: 14 de daño por corte.");
            Add("Tempestade de Lâminas", "Blade Storm", "Tormenta de Hojas");
            Add("Quatro lâminas numa órbita mais larga: 18 de dano por corte.", "Four blades on a wider orbit: 18 damage per cut.", "Cuatro hojas en una órbita más amplia: 18 de daño por corte.");
            Add("Estilhaços", "Shrapnel", "Esquirlas");
            Add("Inimigos abatidos explodem em 3 estilhaços: 6 de dano cada.", "Destroyed enemies burst into 3 shards: 6 damage each.", "Los enemigos destruidos estallan en 3 esquirlas: 6 de daño cada una.");
            Add("Chuva de Estilhaços", "Shrapnel Rain", "Lluvia de Esquirlas");
            Add("Inimigos abatidos explodem em 4 estilhaços: 9 de dano cada.", "Destroyed enemies burst into 4 shards: 9 damage each.", "Los enemigos destruidos estallan en 4 esquirlas: 9 de daño cada una.");
            Add("Tempestade de Estilhaços", "Shrapnel Storm", "Tormenta de Esquirlas");
            Add("Inimigos abatidos explodem em 6 estilhaços: 12 de dano cada.", "Destroyed enemies burst into 6 shards: 12 damage each.", "Los enemigos destruidos estallan en 6 esquirlas: 12 de daño cada una.");
            Add("Disparo Diagonal", "Diagonal Shot", "Disparo Diagonal");
            Add("Cada rajada também dispara 2 tiros em diagonal com 50% do dano.", "Every volley also fires 2 diagonal shots at 50% damage.", "Cada ráfaga también dispara 2 tiros en diagonal con el 50% del daño.");
            Add("Leque Diagonal", "Diagonal Fan", "Abanico Diagonal");
            Add("Os 2 tiros em diagonal causam 75% do dano.", "The 2 diagonal shots deal 75% damage.", "Los 2 tiros en diagonal causan el 75% del daño.");
            Add("Leque Total", "Full Fan", "Abanico Total");
            Add("Cada rajada dispara 4 tiros em leque com 75% do dano.", "Every volley fires 4 fanned shots at 75% damage.", "Cada ráfaga dispara 4 tiros en abanico con el 75% del daño.");
            Add("Aura de Plasma", "Plasma Aura", "Aura de Plasma");
            Add("Inimigos colados na nave sofrem 8 de dano por segundo.", "Enemies close to the ship take 8 damage per second.", "Los enemigos pegados a la nave reciben 8 de daño por segundo.");
            Add("Coroa de Plasma", "Plasma Crown", "Corona de Plasma");
            Add("Aura maior: 12 de dano por segundo.", "Larger aura: 12 damage per second.", "Aura más grande: 12 de daño por segundo.");
            Add("Sol de Plasma", "Plasma Sun", "Sol de Plasma");
            Add("Aura máxima: 18 de dano por segundo.", "Maximum aura: 18 damage per second.", "Aura máxima: 18 de daño por segundo.");
            Add("Nova Solar", "Solar Nova", "Nova Solar");
            Add("A cada 7s, uma onda de energia atinge os inimigos próximos: 20 de dano.", "Every 7s, an energy wave hits nearby enemies: 20 damage.", "Cada 7s, una onda de energía golpea a los enemigos cercanos: 20 de daño.");
            Add("Erupção Solar", "Solar Flare", "Erupción Solar");
            Add("A cada 6s, uma onda maior: 32 de dano.", "Every 6s, a larger wave: 32 damage.", "Cada 6s, una onda mayor: 32 de daño.");
            Add("Supernova", "Supernova", "Supernova");
            Add("A cada 5s, uma onda ainda maior: 48 de dano.", "Every 5s, an even larger wave: 48 damage.", "Cada 5s, una onda aún mayor: 48 de daño.");
            Add("Ataque Orbital", "Orbital Strike", "Ataque Orbital");
            Add("A cada 5s, um satélite atinge 2 inimigos no alcance: 22 de dano em cada.", "Every 5s, a satellite strikes 2 enemies in range: 22 damage each.", "Cada 5s, un satélite golpea a 2 enemigos en alcance: 22 de daño a cada uno.");
            Add("Bombardeio Orbital", "Orbital Bombardment", "Bombardeo Orbital");
            Add("A cada 4,5s, atinge 3 inimigos: 28 de dano em cada.", "Every 4.5s, strikes 3 enemies: 28 damage each.", "Cada 4,5s, golpea a 3 enemigos: 28 de daño a cada uno.");
            Add("Chuva Orbital", "Orbital Rain", "Lluvia Orbital");
            Add("A cada 4s, atinge 4 inimigos: 36 de dano em cada.", "Every 4s, strikes 4 enemies: 36 damage each.", "Cada 4s, golpea a 4 enemigos: 36 de daño a cada uno.");
            Add("Ímã Gravitacional", "Gravity Magnet", "Imán Gravitacional");
            Add("Ganha 15% a mais de XP.", "Gain 15% more XP.", "Ganas un 15% más de XP.");
            Add("Campo Gravitacional", "Gravity Field", "Campo Gravitacional");
            Add("Ganha 30% a mais de XP.", "Gain 30% more XP.", "Ganas un 30% más de XP.");
            Add("Singularidade Magnética", "Magnetic Singularity", "Singularidad Magnética");
            Add("Ganha 50% a mais de XP.", "Gain 50% more XP.", "Ganas un 50% más de XP.");
            Add("Nanorreparo", "Nano Repair", "Nanorreparación");
            Add("Recupera 0,4% do HP máximo por segundo.", "Recover 0.4% of max HP per second.", "Recupera el 0,4% de la vida máxima por segundo.");
            Add("Enxame Nanorreparador", "Repair Swarm", "Enjambre Reparador");
            Add("Recupera 0,7% do HP máximo por segundo.", "Recover 0.7% of max HP per second.", "Recupera el 0,7% de la vida máxima por segundo.");
            Add("Regeneração Total", "Total Regeneration", "Regeneración Total");
            Add("Recupera 1% do HP máximo por segundo.", "Recover 1% of max HP per second.", "Recupera el 1% de la vida máxima por segundo.");
            Add("Núcleo Fênix", "Phoenix Core", "Núcleo Fénix");
            Add("Uma vez por partida, ao ser destruída, a nave renasce com 30% do HP.", "Once per run, when destroyed, the ship is reborn with 30% HP.", "Una vez por partida, al ser destruida, la nave renace con el 30% de vida.");
            Add("Núcleo Fênix Ardente", "Blazing Phoenix Core", "Núcleo Fénix Ardiente");
            Add("Renasce uma vez com 45% do HP e empurra os inimigos próximos.", "Reborn once with 45% HP, pushing nearby enemies away.", "Renace una vez con el 45% de vida y empuja a los enemigos cercanos.");
            Add("Renascer Estelar", "Stellar Rebirth", "Renacer Estelar");
            Add("Renasce uma vez com 60% do HP, empurra e causa 60 de dano ao redor.", "Reborn once with 60% HP, pushing and dealing 60 damage around it.", "Renace una vez con el 60% de vida, empuja y causa 60 de daño alrededor.");
            Add("Adrenalina", "Adrenaline", "Adrenalina");
            Add("Cada abate dá +4% de cadência por 3s, acumulando até 5 vezes.", "Each kill grants +4% fire rate for 3s, stacking up to 5 times.", "Cada eliminación da +4% de cadencia durante 3s, acumulable hasta 5 veces.");
            Add("Adrenalina Pura", "Pure Adrenaline", "Adrenalina Pura");
            Add("+5% de cadência por abate, acumulando até 6 vezes.", "+5% fire rate per kill, stacking up to 6 times.", "+5% de cadencia por eliminación, acumulable hasta 6 veces.");
            Add("Frenesi", "Frenzy", "Frenesí");
            Add("+6% de cadência por abate, acumulando até 8 vezes.", "+6% fire rate per kill, stacking up to 8 times.", "+6% de cadencia por eliminación, acumulable hasta 8 veces.");
            Add("Tiro de Misericórdia", "Mercy Shot", "Tiro de Gracia");
            Add("Tiros do canhão destroem na hora inimigos com até 12% do HP (exceto o chefe).", "Cannon shots instantly destroy enemies at or below 12% HP (except the boss).", "Los disparos del cañón destruyen al instante a enemigos con hasta el 12% de vida (excepto el jefe).");
            Add("Execução", "Execution", "Ejecución");
            Add("Destroem na hora inimigos com até 18% do HP.", "Instantly destroy enemies at or below 18% HP.", "Destruyen al instante a enemigos con hasta el 18% de vida.");
            Add("Sentença Final", "Final Sentence", "Sentencia Final");
            Add("Destroem na hora inimigos com até 25% do HP.", "Instantly destroy enemies at or below 25% HP.", "Destruyen al instante a enemigos con hasta el 25% de vida.");
            Add("Sobrecarga Furiosa", "Furious Overdrive", "Sobrecarga Furiosa");
            Add("Durante a Sobrecarga ofensiva, o canhão causa +25% de dano.", "During Offensive overdrive, the cannon deals +25% damage.", "Durante la Sobrecarga ofensiva, el cañón causa +25% de daño.");
            Add("Fúria Sobrecarregada", "Overcharged Fury", "Furia Sobrecargada");
            Add("+40% de dano na Sobrecarga; cada abate a prolonga em 0,2s.", "+40% damage during Overdrive; each kill extends it by 0.2s.", "+40% de daño en la Sobrecarga; cada eliminación la prolonga 0,2s.");
            Add("Sobrecarga Infernal", "Infernal Overdrive", "Sobrecarga Infernal");
            Add("+60% de dano na Sobrecarga; cada abate a prolonga em 0,3s.", "+60% damage during Overdrive; each kill extends it by 0.3s.", "+60% de daño en la Sobrecarga; cada eliminación la prolonga 0,3s.");
            Add("Casco Espinhoso", "Thorned Hull", "Casco Espinoso");
            Add("Quando um inimigo toca a nave, mesmo bloqueado, espinhos causam 20 de dano ao redor.", "When an enemy touches the ship, even if blocked, spikes deal 20 damage around it.", "Cuando un enemigo toca la nave, aunque sea bloqueado, las púas causan 20 de daño alrededor.");
            Add("Espinhos Reforçados", "Reinforced Thorns", "Púas Reforzadas");
            Add("Os espinhos causam 35 de dano numa área maior.", "The spikes deal 35 damage over a larger area.", "Las púas causan 35 de daño en un área mayor.");
            Add("Retaliação Total", "Total Retaliation", "Represalia Total");
            Add("Os espinhos causam 55 de dano numa área ainda maior.", "The spikes deal 55 damage over an even larger area.", "Las púas causan 55 de daño en un área aún mayor.");
            Add("Égide Prolongada", "Lasting Aegis", "Égida Prolongada");
            Add("A Égide de emergência dura +1s.", "Emergency aegis lasts +1s.", "La Égida de emergencia dura +1s.");
            Add("Égide Persistente", "Persistent Aegis", "Égida Persistente");
            Add("A Égide de emergência dura +2s.", "Emergency aegis lasts +2s.", "La Égida de emergencia dura +2s.");
            Add("Égide Eterna", "Eternal Aegis", "Égida Eterna");
            Add("A Égide de emergência dura +3s e empurra os inimigos ao ativar.", "Emergency aegis lasts +3s and pushes enemies away when activated.", "La Égida de emergencia dura +3s y empuja a los enemigos al activarse.");
            Add("Escudo Regenerativo", "Regenerative Shield", "Escudo Regenerativo");
            Add("Cada bloqueio de escudo recupera 3% do HP máximo.", "Each shield block restores 3% of max HP.", "Cada bloqueo de escudo recupera el 3% de la vida máxima.");
            Add("Escudo Vital", "Vital Shield", "Escudo Vital");
            Add("Cada bloqueio de escudo recupera 5% do HP máximo.", "Each shield block restores 5% of max HP.", "Cada bloqueo de escudo recupera el 5% de la vida máxima.");
            Add("Escudo Restaurador", "Restoring Shield", "Escudo Restaurador");
            Add("Cada bloqueio de escudo recupera 8% do HP máximo.", "Each shield block restores 8% of max HP.", "Cada bloqueo de escudo recupera el 8% de la vida máxima.");
            Add("Fenda Temporal", "Temporal Rift", "Grieta Temporal");
            Add("A cada 9s, abre uma fenda sobre o maior grupo no alcance: 50% de lentidão por 3s e 10 de dano.", "Every 9s, opens a rift on the largest group in range: 50% slow for 3s and 10 damage.", "Cada 9s, abre una grieta sobre el grupo más grande en alcance: 50% de lentitud durante 3s y 10 de daño.");
            Add("Fenda Ampliada", "Widened Rift", "Grieta Ampliada");
            Add("A cada 8s, fenda maior: 60% de lentidão por 4s e 16 de dano.", "Every 8s, a larger rift: 60% slow for 4s and 16 damage.", "Cada 8s, una grieta mayor: 60% de lentitud durante 4s y 16 de daño.");
            Add("Colapso Temporal", "Temporal Collapse", "Colapso Temporal");
            Add("A cada 7s, fenda máxima: 70% de lentidão por 5s e 24 de dano.", "Every 7s, maximum rift: 70% slow for 5s and 24 damage.", "Cada 7s, grieta máxima: 70% de lentitud durante 5s y 24 de daño.");
            Add("Paradoxo", "Paradox", "Paradoja");
            Add("Cada abate reduz em 0,3s a recarga da Estase temporal.", "Each kill reduces Temporal stasis recharge by 0.3s.", "Cada eliminación reduce 0,3s la recarga de la Estasis temporal.");
            Add("Paradoxo Cíclico", "Cyclic Paradox", "Paradoja Cíclica");
            Add("Cada abate reduz em 0,5s a recarga da Estase temporal.", "Each kill reduces Temporal stasis recharge by 0.5s.", "Cada eliminación reduce 0,5s la recarga de la Estasis temporal.");
            Add("Paradoxo Absoluto", "Absolute Paradox", "Paradoja Absoluta");
            Add("Cada abate reduz em 0,8s a recarga da Estase temporal.", "Each kill reduces Temporal stasis recharge by 0.8s.", "Cada eliminación reduce 0,8s la recarga de la Estasis temporal.");
            Add("Eco Temporal", "Time Echo", "Eco Temporal");
            Add("Acertos do canhão têm 15% de chance de se repetir 0,3s depois com 60% do dano.", "Cannon hits have a 15% chance to repeat 0.3s later at 60% damage.", "Los impactos del cañón tienen un 15% de probabilidad de repetirse 0,3s después con el 60% del daño.");
            Add("Eco Duplo", "Double Echo", "Eco Doble");
            Add("25% de chance de eco, com 80% do dano.", "25% echo chance, at 80% damage.", "25% de probabilidad de eco, con el 80% del daño.");
            Add("Eco Infinito", "Infinite Echo", "Eco Infinito");
            Add("35% de chance de eco, com 100% do dano.", "35% echo chance, at 100% damage.", "35% de probabilidad de eco, con el 100% del daño.");
            Add("Sobrecura", "Overheal", "Sobrecuración");
            Add("Cura além do HP máximo vira escudo: 1 bloqueio a cada 10% de excesso (até 1).", "Healing beyond max HP becomes shield: 1 block per 10% overflow (up to 1).", "La curación por encima de la vida máxima se vuelve escudo: 1 bloqueo por cada 10% de exceso (hasta 1).");
            Add("Sobrecura Blindada", "Armored Overheal", "Sobrecuración Blindada");
            Add("Guarda até 2 bloqueios do excesso de cura.", "Stores up to 2 blocks from overflow healing.", "Guarda hasta 2 bloqueos del exceso de curación.");
            Add("Sobrecura Total", "Total Overheal", "Sobrecuración Total");
            Add("Guarda até 3 bloqueios; cada 7% de excesso gera 1.", "Stores up to 3 blocks; every 7% overflow makes 1.", "Guarda hasta 3 bloqueos; cada 7% de exceso genera 1.");
            Add("Pulso Vital", "Vital Pulse", "Pulso Vital");
            Add("Cada cura libera um pulso que causa 2x o HP recuperado em dano ao redor (1 por segundo).", "Each heal releases a pulse dealing 2x the HP restored as damage around the ship (once per second).", "Cada curación libera un pulso que causa 2x la vida recuperada en daño alrededor (1 por segundo).");
            Add("Onda Vital", "Vital Wave", "Onda Vital");
            Add("O pulso causa 3x o HP recuperado numa área maior.", "The pulse deals 3x the HP restored over a larger area.", "El pulso causa 3x la vida recuperada en un área mayor.");
            Add("Explosão Vital", "Vital Burst", "Explosión Vital");
            Add("O pulso causa 4x o HP recuperado numa área ainda maior.", "The pulse deals 4x the HP restored over an even larger area.", "El pulso causa 4x la vida recuperada en un área aún mayor.");
            Add("Protocolo de Emergência", "Emergency Protocol", "Protocolo de Emergencia");
            Add("Ao cair abaixo de 30% do HP, recupera 20% na hora. Rearma após 40s de combate.", "When dropping below 30% HP, instantly restore 20%. Rearms after 40s of combat.", "Al caer por debajo del 30% de vida, recupera el 20% al instante. Se rearma tras 40s de combate.");
            Add("Protocolo Avançado", "Advanced Protocol", "Protocolo Avanzado");
            Add("Recupera 30% na hora. Rearma após 35s de combate.", "Restore 30% instantly. Rearms after 35s of combat.", "Recupera el 30% al instante. Se rearma tras 35s de combate.");
            Add("Protocolo Total", "Total Protocol", "Protocolo Total");
            Add("Recupera 40% na hora. Rearma após 30s de combate.", "Restore 40% instantly. Rearms after 30s of combat.", "Recupera el 40% al instante. Se rearma tras 30s de combate.");
            Add("Canhão de Trilho", "Railgun", "Cañón de Riel");
            Add("A cada 7s, um feixe cruza a arena rumo ao inimigo mais forte no alcance: 45 de dano em todos na linha.", "Every 7s, a beam crosses the arena towards the toughest enemy in range: 45 damage to everything on the line.", "Cada 7s, un haz cruza la arena hacia el enemigo más fuerte en alcance: 45 de daño a todos en la línea.");
            Add("Trilho Acelerado", "Accelerated Rail", "Riel Acelerado");
            Add("A cada 6s, feixe mais largo: 70 de dano na linha.", "Every 6s, a wider beam: 70 damage on the line.", "Cada 6s, un haz más ancho: 70 de daño en la línea.");
            Add("Trilho Aniquilador", "Annihilator Rail", "Riel Aniquilador");
            Add("A cada 5s, feixe máximo: 100 de dano na linha.", "Every 5s, maximum beam: 100 damage on the line.", "Cada 5s, haz máximo: 100 de daño en la línea.");
            Add("Marcação de Alvo", "Target Lock", "Marcado de Objetivo");
            Add("A cada 4s, marca o inimigo mais forte no alcance: ele sofre +30% de dano de tudo por 4s.", "Every 4s, marks the toughest enemy in range: it takes +30% damage from everything for 4s.", "Cada 4s, marca al enemigo más fuerte en alcance: recibe +30% de daño de todo durante 4s.");
            Add("Marcação Precisa", "Precise Lock", "Marcado Preciso");
            Add("A cada 3,5s: +45% de dano no alvo marcado.", "Every 3.5s: +45% damage on the marked target.", "Cada 3,5s: +45% de daño en el objetivo marcado.");
            Add("Marcação Letal", "Lethal Lock", "Marcado Letal");
            Add("A cada 3s: +60% de dano no alvo marcado.", "Every 3s: +60% damage on the marked target.", "Cada 3s: +60% de daño en el objetivo marcado.");
            Add("Ressonância", "Resonance", "Resonancia");
            Add("Ao ativar a Amplificação de dano, uma onda atinge todos os inimigos no alcance: 50 de dano.", "When Damage amplification activates, a wave hits every enemy in range: 50 damage.", "Al activar la Amplificación de daño, una onda golpea a todos los enemigos en alcance: 50 de daño.");
            Add("Ressonância Ampliada", "Amplified Resonance", "Resonancia Ampliada");
            Add("A onda causa 80 de dano.", "The wave deals 80 damage.", "La onda causa 80 de daño.");
            Add("Ressonância Total", "Total Resonance", "Resonancia Total");
            Add("A onda causa 120 de dano e atordoa por 1s.", "The wave deals 120 damage and stuns for 1s.", "La onda causa 120 de daño y aturde durante 1s.");

            // Enemy and wave labels.
            Add("Meteoro Pequeno", "Small Meteor", "Meteoro Pequeño");
            Add("Meteoro Médio", "Medium Meteor", "Meteoro Mediano");
            Add("Meteoro Grande", "Large Meteor", "Meteoro Grande");
            Add("Batedor", "Scout", "Explorador");
            Add("Interceptador", "Interceptor", "Interceptor");
            Add("Fragata", "Frigate", "Fragata");
            Add("Núcleo Aberrante", "Aberrant Core", "Núcleo Aberrante");
            Add("6 Meteoros Pequenos", "6 Small Meteors", "6 Meteoros Pequeños");
            Add("8 Pequenos + 2 Médios", "8 Small + 2 Medium Meteors", "8 Pequeños + 2 Medianos");
            Add("10 Meteoros Médios", "10 Medium Meteors", "10 Meteoros Medianos");
            Add("4 Grandes + 6 Pequenos", "4 Large + 6 Small Meteors", "4 Grandes + 6 Pequeños");
            Add("10 Meteoros mistos + 2 Batedores", "10 Mixed Meteors + 2 Scouts", "10 Meteoros Mixtos + 2 Exploradores");
            Add("6 Batedores + 6 Médios", "6 Scouts + 6 Medium Meteors", "6 Exploradores + 6 Medianos");
            Add("4 Interceptadores + 8 Pequenos", "4 Interceptors + 8 Small Meteors", "4 Interceptores + 8 Pequeños");
            Add("8 Meteoros Grandes", "8 Large Meteors", "8 Meteoros Grandes");
            Add("6 Interceptadores + 6 Batedores", "6 Interceptors + 6 Scouts", "6 Interceptores + 6 Exploradores");
            Add("MINI-CHEFE: 1 Fragata + 10 Batedores", "MINIBOSS: 1 Frigate + 10 Scouts", "MINIJEFE: 1 Fragata + 10 Exploradores");
            Add("10 Grandes + 4 Interceptadores", "10 Large Meteors + 4 Interceptors", "10 Grandes + 4 Interceptores");
            Add("10 Batedores + 6 Interceptadores", "10 Scouts + 6 Interceptors", "10 Exploradores + 6 Interceptores");
            Add("2 Fragatas + 8 Médios", "2 Frigates + 8 Medium Meteors", "2 Fragatas + 8 Medianos");
            Add("ENXAME DENSO: 12 Interceptadores", "DENSE SWARM: 12 Interceptors", "ENJAMBRE DENSO: 12 Interceptores");
            Add("3 Fragatas + 10 Batedores", "3 Frigates + 10 Scouts", "3 Fragatas + 10 Exploradores");
            Add("16 Meteoros mistos", "16 Mixed Meteors", "16 Meteoros Mixtos");
            Add("4 Fragatas + 8 Interceptadores", "4 Frigates + 8 Interceptors", "4 Fragatas + 8 Interceptores");
            Add("ENXAME MÁXIMO: 20 Batedores", "MAXIMUM SWARM: 20 Scouts", "ENJAMBRE MÁXIMO: 20 Exploradores");
            Add("GAUNTLET: 5 Fragatas + 10 Grandes + 10 Batedores", "GAUNTLET: 5 Frigates + 10 Large Meteors + 10 Scouts", "DESAFÍO: 5 Fragatas + 10 Grandes + 10 Exploradores");
            Add("CHEFE FINAL: Núcleo Aberrante", "FINAL BOSS: Aberrant Core", "JEFE FINAL: Núcleo Aberrante");
        }
    }
}
