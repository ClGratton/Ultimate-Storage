using System.Collections.Generic;
using StorageHandler.Models;

namespace StorageHandler.Scripts {
    public static class DatabaseSeeder {
        public static List<ComponentModel> GetCommonModels() {
            var models = new List<ComponentModel>();

            // Resistors (E12 Series)
            string[] decades = { "10", "12", "15", "18", "22", "27", "33", "39", "47", "56", "68", "82" };
            string[] multipliers = { "", "k", "M" };
            string[] packages = { "0603", "0805", "1206", "THT" };

            foreach (var mult in multipliers) {
                foreach (var val in decades) {
                    foreach (var pkg in packages) {
                        string valueStr = $"{val}{mult}";
                        string modelNum = $"RES-{pkg}-{valueStr}";
                        
                        models.Add(new ComponentModel {
                            Category = "Resistor",
                            ModelNumber = modelNum,
                            Type = $"Film Resistor {pkg}",
                            Value = $"{valueStr}Ω",
                            Description = $"Generic {valueStr}Ω Resistor, {pkg} package, 1/4W or 1/10W",
                            DatasheetLink = ""
                        });
                    }
                }
            }

            // Capacitors (Ceramic)
            string[] capValues = { "10pF", "22pF", "100pF", "1nF", "10nF", "100nF", "1uF", "10uF" };
            foreach (var val in capValues) {
                models.Add(new ComponentModel {
                    Category = "Capacitor",
                    ModelNumber = $"CAP-0603-{val}",
                    Type = "Ceramic",
                    Value = val,
                    Description = $"Multilayer Ceramic Capacitor, 0603, {val}, 50V",
                    DatasheetLink = ""
                });
            }

            // Capacitors (Electrolytic)
            string[] elecValues = { "1uF", "10uF", "47uF", "100uF", "220uF", "470uF", "1000uF" };
            foreach (var val in elecValues) {
                models.Add(new ComponentModel {
                    Category = "Capacitor",
                    ModelNumber = $"CAP-ELEC-{val}",
                    Type = "Electrolytic",
                    Value = val,
                    Description = $"Aluminum Electrolytic Capacitor, {val}, 25V",
                    DatasheetLink = ""
                });
            }

            // LEDs
            string[] colors = { "Red", "Green", "Blue", "Yellow", "White" };
            foreach (var color in colors) {
                models.Add(new ComponentModel {
                    Category = "LED",
                    ModelNumber = $"LED-5MM-{color.ToUpper()}",
                    Type = "Standard LED",
                    Value = color,
                    Description = $"5mm {color} LED, Diffused",
                    DatasheetLink = ""
                });
            }

            // Common ICs
            models.Add(new ComponentModel { Category = "IC", ModelNumber = "NE555", Type = "Timer", Value = "N/A", Description = "Precision Timer DIP-8", DatasheetLink = "https://www.ti.com/lit/ds/symlink/ne555.pdf" });
            models.Add(new ComponentModel { Category = "IC", ModelNumber = "LM358", Type = "Op-Amp", Value = "N/A", Description = "Dual Operational Amplifier DIP-8", DatasheetLink = "https://www.ti.com/lit/ds/symlink/lm358.pdf" });
            models.Add(new ComponentModel { Category = "IC", ModelNumber = "LM7805", Type = "Regulator", Value = "5V", Description = "Voltage Regulator 5V 1A TO-220", DatasheetLink = "https://www.sparkfun.com/datasheets/Components/LM7805.pdf" });
            models.Add(new ComponentModel { Category = "IC", ModelNumber = "ATMEGA328P-PU", Type = "Microcontroller", Value = "N/A", Description = "AVR Microcontroller DIP-28", DatasheetLink = "https://ww1.microchip.com/downloads/en/DeviceDoc/Atmel-7810-Automotive-Microcontrollers-ATmega328P_Datasheet.pdf" });
            models.Add(new ComponentModel { Category = "Transistor", ModelNumber = "2N2222", Type = "NPN", Value = "N/A", Description = "NPN General Purpose Transistor TO-92", DatasheetLink = "https://www.onsemi.com/pdf/datasheet/p2n2222a-d.pdf" });
            models.Add(new ComponentModel { Category = "Transistor", ModelNumber = "BC547", Type = "NPN", Value = "N/A", Description = "NPN General Purpose Transistor TO-92", DatasheetLink = "https://www.sparkfun.com/datasheets/Components/BC546.pdf" });
            models.Add(new ComponentModel { Category = "Diode", ModelNumber = "1N4007", Type = "Rectifier", Value = "1000V", Description = "General Purpose Rectifier Diode 1A 1000V", DatasheetLink = "https://www.vishay.com/docs/88503/1n4001.pdf" });
            models.Add(new ComponentModel { Category = "Diode", ModelNumber = "1N4148", Type = "Switching", Value = "100V", Description = "Small Signal Switching Diode", DatasheetLink = "https://www.vishay.com/docs/81857/1n4148.pdf" });

            return models;
        }

        public static List<ComponentDefinition> GetCommonCategories() {
            return new List<ComponentDefinition> {
                new ComponentDefinition { Name = "Resistor" },
                new ComponentDefinition { Name = "Capacitor" },
                new ComponentDefinition { Name = "Inductor" },
                new ComponentDefinition { Name = "Diode" },
                new ComponentDefinition { Name = "Transistor" },
                new ComponentDefinition { Name = "IC" },
                new ComponentDefinition { Name = "Connector" },
                new ComponentDefinition { Name = "Switch" },
                new ComponentDefinition { Name = "Module" },
                new ComponentDefinition { Name = "Display" },
                new ComponentDefinition { Name = "Battery" },
                new ComponentDefinition { Name = "PCB" },
                new ComponentDefinition { Name = "Wire" },
                new ComponentDefinition { Name = "Hardware" },
                new ComponentDefinition { Name = "Tool" }
            };
        }
    }
}
