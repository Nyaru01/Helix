# Guide de maintenance — Helix Blast

## Rôle de l’application

Helix Blast exécute BLAST+ localement sous Windows pour comparer une séquence requête à une collection de fichiers FASTA. Les données restent sur le poste : aucun FASTA ni résultat BLAST n’est envoyé sur Internet.

Le programme distribué s’appelle `HelixBlast.exe`. L’installeur Windows est `HelixBlast-Setup-x.y.z.exe`.

## Utilisation courante

1. Coller une séquence ou charger un fichier FASTA dans **Query sequence**.
2. Sélectionner le dossier contenant les FASTA de référence.
3. Choisir le programme BLAST et les seuils.
4. Cliquer **RUN BLAST**.
5. Exporter les résultats au format CSV, HTML ou PDF depuis la fenêtre de résultats.

Le bouton **Prepare local indexes** prépare des index BLAST persistants. Ils accélèrent les recherches ultérieures. Si l’indexation échoue, l’application revient automatiquement au mode FASTA direct.

## Fichiers locaux

Helix Blast utilise `%LOCALAPPDATA%\HelixBlast` :

- `blast\` : BLAST+ extrait au premier usage ;
- `databases\` : index locaux des FASTA ;
- `history.json` : historique des analyses ;
- `logs\` : journaux de diagnostic.

Le bouton **About / diagnostics** affiche ces chemins et l’état de BLAST.

## Développement et build

Prérequis : Windows, .NET SDK 8 et Inno Setup 6.

```powershell
./prepare-blast.ps1
dotnet test LocalBlast.Tests/LocalBlast.Tests.csproj -c Release
dotnet publish ./LocalBlast.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./publishfull
& 'C:\Users\Nyaru\AppData\Local\Programs\Inno Setup 6\ISCC.exe' ./installer/HelixBlast.iss
```

L’installeur final est créé dans `dist\HelixBlast-Setup-x.y.z.exe`.

Important : publier l’ensemble du dossier `publishfull`, pas seulement l’exécutable. L’interface WPF a besoin de DLL placées à côté de `HelixBlast.exe`.

## Versionner une publication

Pour chaque version :

1. Modifier la version dans `LocalBlast.Wpf.csproj`.
2. Modifier la même version dans `installer\HelixBlast.iss`.
3. Compiler, tester et générer l’installeur.
4. Commiter le code source et pousser sur GitHub.
5. Créer une GitHub Release `vX.Y.Z` et y téléverser `dist\HelixBlast-Setup-X.Y.Z.exe`.

Exemple :

```powershell
git add .
git commit -m "Release Helix Blast 0.3.5"
git push
gh release create v0.3.5 ./dist/HelixBlast-Setup-0.3.5.exe --repo Nyaru01/Helix --title "Helix Blast 0.3.5" --notes "Description courte des changements."
```

Ne jamais versionner les dossiers `bin`, `obj`, `publish`, `publishfull`, `dist` ou `.build-blast`. Ils rendent le dépôt très lourd. Les binaires de distribution vont dans les **GitHub Releases**, pas dans Git.

## Mises à jour des postes

Le bouton **Check updates** interroge la dernière GitHub Release du dépôt `Nyaru01/Helix`.

- Si une version plus récente existe, l’application propose **Install now**. Elle télécharge elle-même l’installeur officiel dans `%LOCALAPPDATA%\HelixBlast\updates`, l’exécute silencieusement, se ferme puis redémarre sur la nouvelle version.
- Aucune désinstallation ni manipulation du fichier téléchargé n’est nécessaire. Selon la politique Windows et le dossier d’installation, une confirmation UAC peut toutefois rester obligatoire.
- Si le contrôle échoue, vérifier l’accès à `api.github.com` et à GitHub dans le réseau de l’utilisateur.

La version installée est visible dans **About / diagnostics**.

## Rapport HTML

Le rapport HTML est autonome : il s’ouvre dans un navigateur, fonctionne hors ligne, contient une recherche, le tri des colonnes, une synthèse et une commande **Print / Save PDF**. Le bouton **Open** de l’application l’ouvre dans le navigateur par défaut.

## Dépannage rapide

- **BLAST unavailable** : utiliser **Check bundled BLAST** ; vérifier le runtime Visual C++ si le message le demande.
- **Résultats Error** : regarder `%LOCALAPPDATA%\HelixBlast\logs` ; le mode direct FASTA est utilisé si les index échouent.
- **Check updates failed** : vérifier Internet et que la dernière Release GitHub possède bien l’installeur `.exe`.
- **L’application ne démarre pas** : conserver le journal Windows et les logs Helix Blast avant toute réinstallation.
