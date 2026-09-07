# Dym&D Asset Companion — Guida rapida

Questa guida permette di iniziare senza configurazioni particolari. Dym&D Asset Companion è un'applicazione locale per Windows: indicizza le immagini sul computer, aiuta a trovarle e permette di trascinarle a risoluzione completa in Clip Studio Paint.

## Installazione

1. Scarica `Dymnd-Asset-Companion-Setup-2.5.1.exe` dalla pagina della release.
2. Avvia il programma di installazione.
3. Windows SmartScreen potrebbe mostrare un avviso perché il programma non è ancora firmato digitalmente. Verifica di aver scaricato il file dalla release ufficiale prima di continuare.
4. Apri **Dym&D Asset Companion** dal menu Start.

La release non include le immagini di Forgotten Adventures. È necessaria una propria libreria regolarmente ottenuta.

## Aggiungere Forgotten Adventures

1. Premi **Add FA**.
2. Seleziona la cartella `_Assets` della libreria Forgotten Adventures, oppure la cartella che la contiene.
3. Attendi la fine della prima indicizzazione.

**Importante:** la classificazione FA dipende dalla struttura e dai nomi originali della libreria. Non rinominare, spostare o appiattire cartelle e file. Una libreria riorganizzata può produrre risultati mancanti o errati, set di muri incompleti e associazioni dei pennelli non valide.

## Trovare e usare gli asset

- **Browser** cerca nell'intera libreria tramite Category, Type, Subtype, Material, Appearance, Theme e Scene / Use.
- Nella ricerca per nome, parole non racchiuse tra virgolette funzionano come termini AND. `Corner Ashen` trova nomi che contengono entrambe le parole.
- Le virgolette cercano una frase esatta. `"Corner_In"` trova quella sequenza precisa.
- Clicca un riquadro per selezionarlo, quindi trascinalo direttamente in Clip Studio Paint.
- Premi **Q** o **E** per ruotare l'asset selezionato di 90 gradi. Premi **F** per capovolgerlo orizzontalmente.
- Il file originale non viene modificato.

## Planner per gli edifici

1. Apri **Planner**.
2. Scegli il sistema di muri, l'aspetto e il tema desiderati. Lascia su **All** le proprietà che non sono importanti.
3. Se corrispondono più set, il Planner mostra un campione per ciascuno. Clicca quello desiderato per aprire il kit completo.
4. Scegli pavimento e finiture, quindi premi **Populate Build**.
5. I risultati rimangono divisi in Wall Pieces, Floor Textures, Detailing e Trim Set.

Per i pennelli CSP, **Ribbon** deve corrispondere esattamente al nome del pennello o sub tool mostrato in Clip Studio Paint. **Tool** e **Tool Group** sono soltanto note organizzative modificabili. Se l'organizzazione personale di CSP è diversa da quella originale, correggi i campi e scegli **Save for Wall Set**.

## Librerie personalizzate

**Add Other** può indicizzare una cartella di immagini personalizzata, ma questa funzione è sperimentale. Usa nomi di file descrittivi e cartelle semplici come `Walls`, `Floors`, `Doors`, `Roofs` o `Terrain`. La ricerca per nome e il trascinamento sono più affidabili del raggruppamento automatico nel Planner.

## Dati e privacy

L'applicazione non contiene IA in esecuzione, telemetria, analisi d'uso o funzioni di rete. I file della libreria vengono letti sul posto e non vengono rinominati, spostati o eliminati. Indice, impostazioni, miniature e copie trasformate sono conservati separatamente in `%LOCALAPPDATA%\DymndAssetBrowser`.

Per maggiori dettagli consulta la guida completa e `AI-DISCLOSURE.md` incluse nella release.
