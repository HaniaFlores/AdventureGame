// Nightfall: A Text Adventure

// How to run (CLI):
//   cd Nightfall
//   dotnet run
//
// Controls during gameplay:
//   - Choose options with A/B (or the listed letters)
//   - S = Save, L = Load, Q = Quit

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

// ==========================
// Structures & "Union" demo
// ==========================

/// <summary>
/// Player statistics kept as a struct (value type).
/// Using a struct here is appropriate because it's a small bundle of numeric fields
/// with value semantics that we pass around as part of GameState.
/// </summary>
public struct Stats
{
    public int MaxHealth;
    public int Health;
    public int Attack;

    public Stats(int maxHealth, int attack)
    {
        MaxHealth = maxHealth;
        Health = maxHealth;
        Attack = attack;
    }
}

/// <summary>
/// Demonstrates a C#-style "union" using Explicit layout:
/// Flat (int) and Percent (float) share the same memory at offset 0.
/// We use this in combat to pick either a flat enemy damage or a percent-based damage.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct DamageUnion
{
    [FieldOffset(0)] public int Flat;
    [FieldOffset(0)] public float Percent;
}

// ==========================
// Core Game State
// ==========================

/// <summary>
/// Holds all mutable state for the current playthrough:
/// player stats, inventory, and whether the game is over.
/// </summary>
public class GameState
{
    // Initialize with reasonable defaults: 10 HP, 3 ATK.
    public Stats Stats = new Stats(maxHealth: 10, attack: 3);
    
    // Inventory is a set to prevent duplicates and allow fast lookup.
    public HashSet<string> Inventory { get; set; } = new();
    
    // Used by the main loop to terminate the run.
    public bool IsGameOver { get; set; } = false;
}

/// <summary>
/// Immutable data describing a single choice in a scene.
/// We use records for brevity and value semantics.
/// </summary>
public record Choice(
    string Key,             // Key the player presses (e.g., "A" or "B")
    string Text,            // Text describing the choice
    string NextId,          // ID of the next scene to navigate to
    int HealthDelta = 0,    // Optional health adjustment from taking the choice
    string? GainItem = null,    // Optional item to add
    string? LoseItem = null     // Optional item to remove
);

// ==========================
// Scene Hierarchy (OOP)
// ==========================

/// <summary>
/// Abstract base class for all scenes. Encapsulates shared properties and behavior.
/// </summary>
public abstract class SceneBase
{
    public string Id { get; }
    public string Text { get; }
    public List<Choice> Choices { get; }

    protected SceneBase(string id, string text, List<Choice> choices)
    {
        Id = id;
        Text = text;
        Choices = choices;
    }


    /// <summary>
    /// Applies the effects of a selected choice to the GameState
    /// and returns the next scene ID. Derived classes can override
    /// to inject special logic (e.g., combat).
    /// </summary>
    public virtual string Apply(GameState s, Choice selected)
    {
        // Variables & Expressions: HP updates, adds/removes items
        s.Stats.Health += selected.HealthDelta;
        if (selected.GainItem is not null) s.Inventory.Add(selected.GainItem);
        if (selected.LoseItem is not null) s.Inventory.Remove(selected.LoseItem);

        // Conditional: if HP falls to 0 or less, go to lose state
        if (s.Stats.Health <= 0) return "lose";

        // Otherwise proceed to the next scene
        return selected.NextId;
    }
}

/// <summary>
/// A simple dialog-only scene. Inherits base behavior without changes,
/// proving inheritance even for the "boring" case.
/// </summary>
public class DialogueScene : SceneBase
{
    public DialogueScene(string id, string text, List<Choice> choices) : base(id, text, choices) {}
}

/// <summary>
/// A scene that performs one round of combat before applying the normal choice effects.
/// Demonstrates polymorphism via overriding Apply().
/// </summary>
public class CombatScene : SceneBase
{
    // Basic enemy attributes
    private readonly int enemyHealth;   
    private readonly int enemyAttack;
    private readonly bool percentBasedEnemyAttack;

    public CombatScene(string id, string text, List<Choice> choices, int enemyHealth = 6, int enemyAttack = 2, bool percentBased = false)
        : base(id, text, choices)
    {
        this.enemyHealth = enemyHealth;
        this.enemyAttack = enemyAttack;
        this.percentBasedEnemyAttack = percentBased;
    }

    /// <summary>
    /// Executes a single combat exchange:
    /// - Player hits; if enemy "dies", enemy deals 0 and you gain a Relic.
    /// - Else enemy hits (flat OR percent, using union).
    /// Then applies the selected choice via base.Apply().
    /// </summary>
    public override string Apply(GameState s, Choice selected)
    {
        // Compute player damage (simple demo: equal to Attack, minimum 1).
        int playerDamage = Math.Max(1, s.Stats.Attack);

        // Determine if the enemy would drop to 0 this round
        int enemyRemaining = enemyHealth - playerDamage;

        // Compute enemy damage using the union
        int enemyDamage;
        var dmg = new DamageUnion();

        if (enemyRemaining <= 0)
        {
            // Enemy "dies": deals 0, and we reward the player with a Relic once
            enemyDamage = 0;
            Console.WriteLine("\n— Combat Round —");
            Console.WriteLine($"You strike for {playerDamage}. The shadow dissipates!");
            Console.WriteLine("The fallen guardian leaves behind a Relic.");
            s.Inventory.Add("Relic"); // reward for lethal hit
        }
        else
        {
            // Enemy is still alive: it hits back using either flat or percent damage
            if (percentBasedEnemyAttack)
            {
                // Percent path (uses union Percent)
                dmg.Percent = 0.25f; // 25% of current HP
                enemyDamage = Math.Max(1, (int)Math.Floor(s.Stats.Health * dmg.Percent));
            }
            else
            {
                // Flat path (uses union Flat)
                dmg.Flat = enemyAttack;
                enemyDamage = dmg.Flat;
            }

            Console.WriteLine("\n— Combat Round —");
            Console.WriteLine($"You strike for {playerDamage}. (Enemy HP ≈ {enemyRemaining})");
            Console.WriteLine($"Enemy strikes for {enemyDamage}.");
        }

        // Apply enemy damage to player
        s.Stats.Health -= enemyDamage;
        if (s.Stats.Health <= 0)
            return "lose";

        // Then apply standard choice effects (health deltas, items, next scene)
        return base.Apply(s, selected);
    }
}

// ==========================
// Game Engine (loop & flow)
// ==========================
public static class Program
{
    // Scene registry
    static readonly Dictionary<string, SceneBase> Scenes = new()
    {
        ["start"] = new DialogueScene(
            "start",
            "You wake in a quiet forest. A path splits left (A) and right (B).",
            new()
            {
                new("A", "Go left toward a cabin.", "cabin"),
                new("B", "Go right toward the sound of water.", "river")
            }
        ),

        ["cabin"] = new DialogueScene(
            "cabin",
            "A small cabin with the door ajar. Enter (A) or search the yard (B)?",
            new()
            {
                // GainItem used; HealthDelta used
                new("A", "Enter the cabin.", "cave", HealthDelta:+2, GainItem:"Key"),
                new("B", "Search the yard.", "cave", HealthDelta:-2, GainItem:"Torch")
            }
        ),

        ["river"] = new DialogueScene(
            "river",
            "A fast river blocks your way. Jump (A) or follow upstream (B)?",
            new()
            {
                // Jumping triggers a FLAT-damage combat (beast)
                new("A", "Jump across.", "beast"),
                new("B", "Follow upstream to a shallow crossing.", "cave")
            }
        ),

        // New combat scene demonstrating FLAT union branch
        ["beast"] = new CombatScene(
            "beast",
            "A river-beast surfaces with a hiss! Will you fight or back away?",
            new()
            {
                // After the exchange, continue on your way to the cave
                new("A", "Push forward toward the cave.", "cave")
            },
            enemyHealth: 4,   // low HP; often killed in one hit => get Relic
            enemyAttack: 2,   // flat damage path used here
            percentBased: false
        ),

        ["cave"] = new DialogueScene(
            "cave",
            "At dusk, you reach a cave with a locked gate. Open (A) or camp (B)?",
            new()
            {
                new("A", "Try to open the gate.", "gate"),
                // Rest: small heal, but no item change here
                new("B", "Camp outside to recover.", "cabin", HealthDelta:+1)
            }
        ),

        // Combat scene demonstrating PERCENT union branch + LoseItem on forcing
        ["gate"] = new CombatScene(
            "gate",
            "A shadow guards the locked gate. You may try the key (A) or force the gate (B).",
            new()
            {
                // Path A: requires Key (checked in main loop); if lethal, you still get Relic here
                new("A", "Use the key if you have it.", "win"),
                // Path B: you drop your Torch while struggling (LoseItem used)
                new("B", "Force it open.", "maybe_lose", HealthDelta:-1, GainItem:null, LoseItem:"Torch")
            },
            enemyHealth: 6,    // tougher; percent damage stings if you're at high HP
            enemyAttack: 2,    // not used when percentBased==true; kept for completeness
            percentBased: true // use Percent path here (union.Percent)
        ),

        ["maybe_lose"] = new DialogueScene(
            "maybe_lose",
            "Pain shoots through your arms.",
            new()
            {
                new("A", "Stagger back to the cabin.", "cabin"),
                // Collapsing makes you drop the Key if you had it (LoseItem used again)
                new("B", "Collapse where you stand.", "lose", 0, null, "Key")
            }
        ),

        ["win"] = new DialogueScene(
            "win",
            "The key turns. The shrine glows. You win!",
            new()
        ),

        ["lose"] = new DialogueScene(
            "lose",
            "Darkness falls. Game over.",
            new()
        ),
    };

    /// <summary>
    /// Program entry point. Shows title, then runs the main game loop until game over.
    /// </summary>
    static void Main()
    {
        var state = new GameState();
        var currentId = "start";

        Title();

         // ================
        // Main Game Loop
        // ================
        while (!state.IsGameOver)
        {
            Console.Clear();
            ShowHUD(state);
            var scene = Scenes[currentId];
            Console.WriteLine(scene.Text);

            // Handle terminal scenes (win/lose) directly.
            if (scene.Id is "win" or "lose")
            {
                state.IsGameOver = true;
                break;
            }

            // Contextual hint: if you're at the gate WITH a key, tell the player.
            if (scene.Id == "gate" && state.Inventory.Contains("Key"))
                Console.WriteLine("[Hint] You have a Key.");

            // Render choices
            foreach (var ch in scene.Choices)
                Console.WriteLine($"{ch.Key}) {ch.Text}");

            // Render global commands for save/load/quit
            Console.WriteLine("\nCommands: S) Save   L) Load   Q) Quit");

            // Read a valid choice/command from the user.
            var input = ReadInput(scene.Choices.Select(c => c.Key));

            // Global command handling (File I/O lives here).
            if (input == "S")
            {
                SaveGame(state, currentId);
                Pause("Saved.");
                continue;
            }
            if (input == "L")
            {
                if (LoadGame(out var loaded, out var loadedId))
                {
                    state = loaded!;
                    currentId = loadedId!;
                    Pause("Loaded.");
                }
                else Pause("No save found.");
                continue;
            }
            if (input == "Q")
            {
                Console.WriteLine("Quitting…");
                return;
            }

            // We have a scene-specific choice; find the matching Choice object.
            var choice = scene.Choices.First(c => c.Key.Equals(input, StringComparison.OrdinalIgnoreCase));

            // Special rule: Using the key at the gate requires actually having it.
            if (scene.Id == "gate" && choice.Key.Equals("A", StringComparison.OrdinalIgnoreCase) && !state.Inventory.Contains("Key"))
            {
                Console.WriteLine("\nYou fumble for a key… you don’t have one.");
                Pause();
                currentId = "maybe_lose";
                continue;
            }

            // Polymorphism in action: Apply() is overridden by CombatScene.
            currentId = scene.Apply(state, choice);

            // If HP went to zero while applying effects, go to lose.
            if (state.Stats.Health <= 0) currentId = "lose";
        }

        End(state);
    }

    // ==========================
    // Utility / Helper functions
    // ==========================

    /// <summary>
    /// Shows the title splash and waits for Enter.
    /// </summary>
    static void Title()
    {
        Console.WriteLine("=== Nightfall: Text Adventure ===");
        Console.WriteLine("Press Enter to begin...");
        Console.ReadLine();
        Console.Clear();
    }

     /// <summary>
    /// Heads-up display: HP, ATK, and inventory, plus a divider line.
    /// Keeping UI in a helper keeps the main loop clean and readable.
    /// </summary>
    static void ShowHUD(GameState s)
    {
        Console.WriteLine($"HP: {s.Stats.Health}/{s.Stats.MaxHealth}   ATK: {s.Stats.Attack}   Inventory: [{string.Join(", ", s.Inventory.DefaultIfEmpty("-"))}]");
        Console.WriteLine(new string('-', 60));
    }

    /// <summary>
    /// Reads input until the user types one of the allowed keys or a global command (S/L/Q).
    /// </summary>
    static string ReadInput(IEnumerable<string> allowedKeys)
    {
        // Normalize the allowed choices to uppercase for easy comparison.
        var allowed = new HashSet<string>(allowedKeys.Select(k => k.ToUpperInvariant())) { "S", "L", "Q" };
        
        // Loop until valid input is received (input-validation loop).
        while (true)
        {
            Console.Write($"Choose [{string.Join("/", allowed)}]: ");
            var input = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (allowed.Contains(input)) return input;
            Console.WriteLine("Invalid choice/command.");
        }
    }

    /// <summary>
    /// Simple "Press Enter" pause that can also show a specific message.
    /// </summary>
    static void Pause(string msg = "Press Enter to continue...")
    {
        Console.WriteLine(msg);
        Console.ReadLine();
    }

    // ==========================
    // File I/O: Save / Load
    // ==========================

    /// <summary>
    /// Serializes the game state + current scene to a JSON file (save.json).
    /// Demonstrates file output and JSON serialization.
    /// </summary>
    static void SaveGame(GameState s, string currentId)
    {
        var save = new SaveData
        {
            CurrentScene = currentId,
            Stats = s.Stats,
            Inventory = s.Inventory.ToArray()
        };
        var json = JsonSerializer.Serialize(save, new JsonSerializerOptions { WriteIndented = true });

        // WriteAllText creates or overwrites the file atomically for this small payload.
        File.WriteAllText("save.json", json);
    }

    /// <summary>
    /// Attempts to read save.json and hydrate a GameState + current scene.
    /// Returns true if load succeeded, false otherwise.
    /// </summary>
    static bool LoadGame(out GameState? state, out string? currentId)
    {
        state = null;
        currentId = null;

        // If there's no save file, fail gracefully.
        if (!File.Exists("save.json")) return false;

        var json = File.ReadAllText("save.json");
        var save = JsonSerializer.Deserialize<SaveData>(json);
        if (save is null) return false;

        state = new GameState
        {
            Stats = save.Stats,
            Inventory = new HashSet<string>(save.Inventory ?? Array.Empty<string>())
        };
        currentId = save.CurrentScene ?? "start";
        return true;
    }

    /// <summary>
    /// Private DTO for persistence. Keeping it separate decouples
    /// the on-disk shape from runtime types if we change internals later.
    /// </summary>
    private class SaveData
    {
        public string? CurrentScene { get; set; }
        public Stats Stats { get; set; }
        public string[]? Inventory { get; set; }
    }

    static void End(GameState s)
    {
        Console.WriteLine($"\nFinal HP: {s.Stats.Health}/{s.Stats.MaxHealth}");
        Console.WriteLine($"Inventory: {string.Join(", ", s.Inventory.DefaultIfEmpty("-"))}");
        Console.WriteLine("Thanks for playing!");
    }
}

