using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SimulationGame
{
    /// <summary>
    /// Represents a single, self-contained narrative event in the game.
    /// This ScriptableObject holds all the data needed to present an event to the player,
    /// including its text, conditions, and the choices it offers.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEvent", menuName = "Narrative Framework/Event")]
    public class SimEventSO : NarrativeObject
    {
        /// <summary>
        /// Defines the category or purpose of the event, which affects how it's handled by the EventSystem.
        /// </summary>
        public enum EventType
        {
            /// <summary>A standard event that is part of the main storyline.</summary>
            Main,
            /// <summary>A conditional, repeatable event that can occur randomly.</summary>
            Random,
            /// <summary>A crucial story beat that must occur.</summary>
            Critical,
            /// <summary>An event that concludes the game.</summary>
            Ending
        }

        [Header("Core Content")]
        [Tooltip("A unique identifier for this event, used for saving and external references.")]
        [SerializeField] public string simEventId;
        [Tooltip("A short, designer-facing numeric ID, often assigned by the Narrative Linker.")]
        [SerializeField] public int simEventNumericId;
        [Tooltip("The title of the event, displayed to the player.")]
        [SerializeField] public string simEventTitle;
        [Tooltip("The main descriptive text for the event.")]
        [TextArea(4, 10)]
        [SerializeField] public string simEventDescription;

        [Header("Visuals & Audio")]
        [Tooltip("The primary illustration or character portrait for this event.")]
        [SerializeField] public Sprite mainIllustration;
        [Tooltip("An optional background image for the event scene.")]
        [SerializeField] public Sprite backgroundImage;
        [Tooltip("An optional ambient sound clip to play during the event.")]
        [SerializeField] public AudioClip ambientSound;

        [Header("Scheduling & Conditions")]
        [Tooltip("The first day this event can occur.")]
        [Range(1, 365)] public int MinTimeUnit = 1;
        [Tooltip("The last day this event can occur.")]
        [Range(1, 365)] public int MaxTimeUnit = 30;
        [Tooltip("The base probability (0.0 to 1.0) that this event will occur if its conditions are met.")]
        [Range(0f, 1f)] public float Probability = 1.0f;
        [Tooltip("The category of the event, which determines how it is selected and handled.")]
        public EventType Type = EventType.Main;

        [Header("Narrative Structure")]
        [Tooltip("The Story Arc this event belongs to, if any.")]
        public StoryArcSO StoryArc;
        [Tooltip("A list of other events that are directly linked from this one (e.g., for sequence building).")]
        public List<SimEventSO> LinkedEvents = new();

        [Header("Logic & Choices")]
        [Tooltip("A list of flags that must be present in the game state for this event to be available.")]
        [SerializeField] public List<string> requiredFlags = new();
        [Tooltip("A list of flags that must NOT be present in the game state for this event to be available.")]
        [SerializeField] private List<string> forbiddenFlags = new();
        [Tooltip("Descriptive tags for organizing or filtering events (e.g., 'Combat', 'Diplomacy').")]
        [SerializeField] public List<string> storyTags = new();
        [Tooltip("The list of choices the player can make during this event.")]
        [SerializeField] public List<Choice> choices = new();

        [Header("Actors")]
        [Tooltip("A list of characters or entities involved in this event.")]
        public List<ActorSO> InvolvedActors = new();

        #region Public Properties
        public string SimEventId => simEventId;
        public int SimEventNumericId => simEventNumericId;
        public string SimEventTitle => simEventTitle;
        public string SimEventDescription => simEventDescription;

        public Sprite MainIllustration => mainIllustration;
        public Sprite BackgroundImage => backgroundImage;
        public AudioClip AmbientSound => ambientSound;

        public List<string> RequiredFlags => requiredFlags;
        public List<string> ForbiddenFlags
        {
            get => forbiddenFlags;
            set => forbiddenFlags = value;
        }
        public List<string> StoryTags => storyTags;
        public List<Choice> Choices => choices;
        #endregion

        /// <summary>
        /// Checks if this event is available to be triggered on a given day with the current set of flags.
        /// </summary>
        public bool IsAvailable(int currentDay, IEnumerable<string> flags)
        {
            if (currentDay < MinTimeUnit || currentDay > MaxTimeUnit)
                return false;

            var flagSet = new HashSet<string>(flags);
            foreach (var flag in requiredFlags)
            {
                if (!flagSet.Contains(flag))
                    return false;
            }

            foreach (var flag in forbiddenFlags)
            {
                if (flagSet.Contains(flag))
                    return false;
            }

            return true;
        }

        /// <summary>Determines if the event should occur based on its probability.</summary>
        public bool ShouldOccur() => Random.value <= Probability;

        /// <summary>
        /// Editor-friendly accessor treated as the Outcomes of the first Choice.
        /// Getter zwraca listę, jeśli to możliwe (bez kopiowania); inaczej kopię.
        /// Setter aktualizuje prywatne pole listy w Choice (readonly property) przez refleksję.
        /// </summary>
        public List<Outcome> Outcomes
        {
            get
            {
                if (choices == null || choices.Count == 0 || choices[0] == null) return null;

                var ch = choices[0];
                // 1) Spróbuj pobrać przez property "Outcomes" (IReadOnlyList<Outcome>)
                var p = typeof(Choice).GetProperty("Outcomes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    var ro = p.GetValue(ch) as IEnumerable<Outcome>;
                    if (ro == null) return null;
                    // jeśli jest to już List<Outcome>, oddaj referencję
                    if (ro is List<Outcome> listRef) return listRef;
                    // inaczej zrób kopię (nie mamy referencji do prawdziwej listy)
                    return ro.ToList();
                }

                // 2) Spróbuj pobrać prywatne pole listy
                var f = FindOutcomesField();
                if (f != null)
                {
                    var v = f.GetValue(ch) as List<Outcome>;
                    return v;
                }

                return null;
            }
            set
            {
                if (value == null) return;

                if (choices == null) choices = new List<Choice>();
                if (choices.Count == 0 || choices[0] == null)
                    choices.Insert(0, new Choice());

                var ch = choices[0];

                // 1) jeśli Choice ma prywatne pole listy – ustawiamy je bezpośrednio
                var f = FindOutcomesField();
                if (f != null)
                {
                    f.SetValue(ch, value);
                    return;
                }

                // 2) jeśli nie ma pola, ale property "Outcomes" istnieje (read-only),
                // to spróbujemy zmutować listę pośrednio: wyczyść i dodaj (gdy to List<>),
                // inaczej niestety nie ma legalnej drogi bez modyfikacji klasy Choice.
                var p = typeof(Choice).GetProperty("Outcomes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    var ro = p.GetValue(ch) as IEnumerable<Outcome>;
                    if (ro is List<Outcome> listRef)
                    {
                        listRef.Clear();
                        listRef.AddRange(value);
                        return;
                    }
                }

                // 3) Ostateczny fallback – nic nie zrobimy bez zmiany Choice.
                Debug.LogWarning("[SimEventSO] Could not set Outcomes: neither a writable field nor a mutable list was found on Choice. " +
                                 "Consider exposing a setter or a backing field on Choice.");
            }
        }

        /// <summary>
        /// Helper to locate likely backing field for Choice.Outcomes.
        /// </summary>
        private static FieldInfo FindOutcomesField()
        {
            var t = typeof(Choice);
            // typowe nazwy pól
            var names = new[] { "_outcomes", "outcomes", "m_Outcomes" };
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null && typeof(List<Outcome>).IsAssignableFrom(f.FieldType))
                    return f;
            }
            // fallback: pierwsze pole List<Outcome> w klasie
            var any = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                       .FirstOrDefault(fi => typeof(List<Outcome>).IsAssignableFrom(fi.FieldType));
            return any;
        }
    }
}
