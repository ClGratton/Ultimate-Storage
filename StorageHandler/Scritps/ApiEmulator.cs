using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StorageHandler.Models;

namespace StorageHandler.Scripts {
    public static class ApiEmulator {
        public static void GenerateTestFiles(string databaseRoot) {
            string componentsDir = Path.Combine(databaseRoot, "Components");
            if (!Directory.Exists(componentsDir)) {
                Directory.CreateDirectory(componentsDir);
            }

            // 1. Electronics Data
            var electronics = new List<Dictionary<string, string>>();
            
            // Resistors (0603, 0805, 1206)
            var resistorValues = new Dictionary<string, string> {
                { "10", "10R" }, { "22", "22R" }, { "47", "47R" },
                { "100", "100R" }, { "220", "220R" }, { "330", "330R" }, { "470", "470R" },
                { "1k", "1K" }, { "2.2k", "2K2" }, { "4.7k", "4K7" }, { "10k", "10K" },
                { "22k", "22K" }, { "47k", "47K" }, { "100k", "100K" }, { "1M", "1M" }
            };

            var packages = new[] { "0603", "0805", "1206" };

            foreach (var pkg in packages) {
                foreach (var kvp in resistorValues) {
                    electronics.Add(new Dictionary<string, string> {
                        { "id", $"RC{pkg}FR-07{kvp.Value}L" },
                        { "category", "Resistor" },
                        { "value", kvp.Key },
                        { "package", pkg },
                        { "power", pkg == "0603" ? "0.1W" : (pkg == "0805" ? "0.125W" : "0.25W") },
                        { "description", $"Resistor {kvp.Key} 1% {pkg}" }
                    });
                }
            }

            // Capacitors
            var caps = new List<(string val, string pkg, string volt, string type)> {
                ("10pF", "0603", "50V", "C0G"), ("22pF", "0603", "50V", "C0G"),
                ("100nF", "0603", "50V", "X7R"), ("100nF", "0805", "50V", "X7R"),
                ("1uF", "0603", "16V", "X5R"), ("1uF", "0805", "25V", "X5R"),
                ("10uF", "0805", "10V", "X5R"), ("10uF", "1206", "25V", "X5R"),
                ("100uF", "Radial", "25V", "Electrolytic"), ("470uF", "Radial", "16V", "Electrolytic")
            };

            foreach (var cap in caps) {
                electronics.Add(new Dictionary<string, string> {
                    { "id", $"CC{cap.pkg}-{cap.val}-{cap.volt}" }, // Fake ID generation
                    { "category", "Capacitor" },
                    { "value", cap.val },
                    { "package", cap.pkg },
                    { "voltage", cap.volt },
                    { "type", cap.type },
                    { "description", $"{cap.type} Capacitor {cap.val} {cap.volt} {cap.pkg}" }
                });
            }

            // LEDs
            var leds = new[] { "Red", "Green", "Blue", "Yellow", "White" };
            foreach (var color in leds) {
                electronics.Add(new Dictionary<string, string> {
                    { "id", $"LED-0603-{color.ToUpper()}" },
                    { "category", "LED" },
                    { "color", color },
                    { "package", "0603" },
                    { "description", $"SMD LED {color} 0603" }
                });
                electronics.Add(new Dictionary<string, string> {
                    { "id", $"LED-5MM-{color.ToUpper()}" },
                    { "category", "LED" },
                    { "color", color },
                    { "package", "5mm THT" },
                    { "description", $"Through-hole LED {color} 5mm" }
                });
            }

            // ICs & Transistors
            var activeComponents = new List<Dictionary<string, string>> {
                new Dictionary<string, string> { { "id", "NE555P" }, { "category", "IC" }, { "package", "DIP-8" }, { "description", "Precision Timer" } },
                new Dictionary<string, string> { { "id", "LM358N" }, { "category", "IC" }, { "package", "DIP-8" }, { "description", "Dual Operational Amplifier" } },
                new Dictionary<string, string> { { "id", "ATMEGA328P-PU" }, { "category", "IC" }, { "package", "DIP-28" }, { "description", "AVR Microcontroller 8-bit" } },
                new Dictionary<string, string> { { "id", "ESP32-WROOM-32" }, { "category", "Module" }, { "package", "SMD" }, { "description", "Wi-Fi & Bluetooth Module" } },
                new Dictionary<string, string> { { "id", "2N2222" }, { "category", "Transistor" }, { "package", "TO-92" }, { "type", "NPN" }, { "description", "NPN General Purpose Transistor" } },
                new Dictionary<string, string> { { "id", "BC547" }, { "category", "Transistor" }, { "package", "TO-92" }, { "type", "NPN" }, { "description", "NPN General Purpose Transistor" } },
                new Dictionary<string, string> { { "id", "IRFZ44N" }, { "category", "Transistor" }, { "package", "TO-220" }, { "type", "N-Channel MOSFET" }, { "description", "N-Channel Power MOSFET 55V 49A" } },
                new Dictionary<string, string> { { "id", "L7805CV" }, { "category", "Regulator" }, { "package", "TO-220" }, { "output", "5V" }, { "description", "Positive Voltage Regulator 5V 1.5A" } }
            };
            electronics.AddRange(activeComponents);

            string elecJson = JsonSerializer.Serialize(electronics, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(componentsDir, "items_electronics.json"), elecJson);


            // 2. YuGiOh Data
            var yugioh = new List<Dictionary<string, string>>();
            int idCounter = 1;

            void AddCard(string name, string set, string rarity, string type) {
                yugioh.Add(new Dictionary<string, string> {
                    { "id", $"YGO-{idCounter++:D5}" },
                    { "cardName", name },
                    { "setCode", set },
                    { "rarity", rarity },
                    { "type", type }
                });
            }

            // Monsters
            AddCard("Blue-Eyes White Dragon", "LOB-001", "Ultra Rare", "Dragon/Normal");
            AddCard("Dark Magician", "SDY-006", "Ultra Rare", "Spellcaster/Normal");
            AddCard("Red-Eyes Black Dragon", "LOB-070", "Ultra Rare", "Dragon/Normal");
            AddCard("Dark Magician Girl", "MFC-000", "Secret Rare", "Spellcaster/Effect");
            AddCard("Summoned Skull", "MRD-003", "Ultra Rare", "Fiend/Normal");
            AddCard("Celtic Guardian", "LOB-007", "Super Rare", "Warrior/Normal");
            AddCard("Gaia The Fierce Knight", "LOB-006", "Ultra Rare", "Warrior/Normal");
            AddCard("Kuriboh", "MRD-071", "Super Rare", "Fiend/Effect");
            
            // Exodia
            AddCard("Exodia the Forbidden One", "LOB-124", "Ultra Rare", "Spellcaster/Effect");
            AddCard("Right Leg of the Forbidden One", "LOB-120", "Ultra Rare", "Spellcaster/Normal");
            AddCard("Left Leg of the Forbidden One", "LOB-121", "Ultra Rare", "Spellcaster/Normal");
            AddCard("Right Arm of the Forbidden One", "LOB-122", "Ultra Rare", "Spellcaster/Normal");
            AddCard("Left Arm of the Forbidden One", "LOB-123", "Ultra Rare", "Spellcaster/Normal");

            // God Cards
            AddCard("Slifer the Sky Dragon", "GB1-001", "Secret Rare", "Divine-Beast/Effect");
            AddCard("Obelisk the Tormentor", "GB1-002", "Secret Rare", "Divine-Beast/Effect");
            AddCard("The Winged Dragon of Ra", "GB1-003", "Secret Rare", "Divine-Beast/Effect");

            // Spells
            AddCard("Pot of Greed", "LOB-119", "Rare", "Spell");
            AddCard("Monster Reborn", "LOB-118", "Ultra Rare", "Spell");
            AddCard("Dark Hole", "LOB-052", "Super Rare", "Spell");
            AddCard("Raigeki", "LOB-053", "Super Rare", "Spell");
            AddCard("Polymerization", "LOB-059", "Rare", "Spell");
            AddCard("Swords of Revealing Light", "LOB-101", "Super Rare", "Spell");

            // Traps
            AddCard("Mirror Force", "MRD-138", "Ultra Rare", "Trap");
            AddCard("Trap Hole", "LOB-058", "Super Rare", "Trap");
            AddCard("Magic Cylinder", "LON-104", "Secret Rare", "Trap");
            AddCard("Call of the Haunted", "PSV-012", "Ultra Rare", "Trap");

            string ygoJson = JsonSerializer.Serialize(yugioh, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(componentsDir, "items_yugioh.json"), ygoJson);
        }
    }
}
