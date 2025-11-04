# 🧰 Application de Test EL418 

![.NET](https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-UI%20Framework-blueviolet)
![Status](https://img.shields.io/badge/Status-Active-success)
![License](https://img.shields.io/badge/License-MIT-lightgrey)

## 🧩 Présentation

**AppTestEL418** est une application **WPF (.NET)** développée dans le cadre du **banc de test EL418** pour **TTS (Trafic Technologie Système)**.  
Elle permet la **communication série (RS232)** avec une carte EL418 afin de tester, valider et diagnostiquer les modules électroniques du système.

L’application a été pensée pour offrir une interface moderne, ergonomique et fiable, facilitant les opérations de test 

---

## 🚀 Fonctionnalités principales

- 🔌 **Communication RS232** : envoi et réception de trames STS via le port série.
- 📡 **Analyse de trames** : extraction automatique des données et états de test.
- 🖥️ **Interface WPF réactive** : mise à jour automatique des indicateurs via `Dispatcher.Invoke`.
- 📊 **Affichage en temps réel** des résultats et états du banc.
- 🧱 **Structure modulaire** prête à évoluer vers des tests automatisés.

---

## 📁 Fichiers utiles

- Lien vers les fichiers de CAO 3D:
- Lien vers les fichiers de CAO électronique:

---

## ⚙️ Prérequis

- Windows 10 ou 11  
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 ou VS Code avec extension C#  
- Un périphérique RS232 (STM32F103 dans le cadre du banc EL418)  
- Câble USB–série (ou adaptateur COM)

---

## 🧪 Utilisation

1. **Lancer l’application**  

2. **Configurer le port COM**  
- Choisir le bon port COM

3. **Démarrer la communication**  
- Cliquer sur “Ouvrir COM”.  
- Les infos de test et les indications s'affichent en temps réel

4. **Analyser les résultats**  
- Les statuts des tests apparaissent sous forme d’indicateurs colorés.  

## 🧠 Notes techniques

- Implémentation basée sur `System.IO.Ports.SerialPort`.  
- Gestion UI thread-safe via `Dispatcher.Invoke()` / `Dispatcher.BeginInvoke()`.  
- Architecture compatible avec un futur découpage **MVVM**.  
- Peut évoluer vers une interface **multi-bancs** ou **multi-protocoles** (CAN, TCP...).

## 👤 Auteur

**Développé par :** Enzo PERRIER 
**Entreprise :** TTS (Trafic Technologie Système)  
**Projet :** Banc de test EL418  

