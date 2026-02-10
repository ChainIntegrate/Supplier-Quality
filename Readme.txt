SUPPLIER QUALITY
Applicazione interna – Avvio rapido

------------------------------------------------------------

LEGGERE PRIMA DI AVVIARE

Durante il primo avvio Windows può mostrare
alcuni messaggi di sicurezza.
Sono NORMALI e previsti.

Seguendo le istruzioni sotto,
l’applicazione funziona correttamente.

------------------------------------------------------------

COS'È

Supplier Quality è un'applicazione interna per la gestione
e valutazione dei fornitori.

- Funziona solo in rete locale (LAN o VPN)
- Non si installa
- Non invia dati all’esterno
- Usa solo file locali

------------------------------------------------------------

DOVE SALVARE LA CARTELLA

Dopo aver estratto lo ZIP, la cartella può essere
salvata in QUALSIASI posizione del computer.

Posizioni CONSIGLIATE:
- Desktop
- Documenti
- C:\SupplierQuality\
- Cartella condivisa su server aziendale (LAN)

Posizioni DA EVITARE:
- Cartelle di sistema (es. C:\Windows\)
- Percorsi con permessi limitati

IMPORTANTE:
L’applicazione salva e modifica file nella cartella "data".
È necessario avere permessi di scrittura.

------------------------------------------------------------

STRUTTURA DELLA CARTELLA

All’interno della cartella troverai:

- SupplierQuality.exe
- Avvia_SupplierQuality.bat
- README.txt
- wwwroot\
- data\
  - suppliers.json
  - evaluations.json
  - backups\

SPIEGAZIONE:
- wwwroot\
  contiene i file dell’interfaccia (HTML, CSS, JS).
  Non contiene dati sensibili.

- data\
  contiene i dati dell’applicazione.

- data\backups\
  contiene le copie di sicurezza automatiche
  dei file dati.

------------------------------------------------------------

COME AVVIARE L’APPLICAZIONE

1. Apri la cartella estratta
2. Fai doppio click su:

   Avvia_SupplierQuality.bat

3. Attendi alcuni secondi
4. L’interfaccia si apre nel browser

------------------------------------------------------------

PRIMO AVVIO – MESSAGGI DI WINDOWS

Al primo avvio Windows può mostrare
alcuni messaggi di sicurezza.

1) AVVISO DI ESECUZIONE FILE

Messaggio tipico:
"Sei sicuro di voler eseguire questo file?"
oppure
"Windows ha protetto il PC"

Questo messaggio NON indica un virus.

Compare perché l’applicazione:
- è interna
- non è firmata digitalmente
- non è installata tramite Microsoft Store

COSA FARE:
- clicca su "Ulteriori informazioni"
- poi su "Esegui comunque"

2) RICHIESTA DI ACCESSO ALLA RETE

Subito dopo, Windows può chiedere
se consentire l’accesso alla rete.

Scegliere:
✔ RETE PRIVATA (ufficio / azienda / VPN)
✖ NON rete pubblica

Questa scelta serve solo per permettere
al browser di raggiungere l’interfaccia locale.
Nessun dato viene inviato su Internet.

------------------------------------------------------------

SE NON SI APRE IL BROWSER

Apri manualmente il browser e vai a:

http://127.0.0.1:8085/

------------------------------------------------------------

CHIUSURA DELL’APPLICAZIONE

Per chiudere:
- chiudi la finestra dell’app
- oppure chiudi il terminale associato

Non resta nulla in esecuzione.

------------------------------------------------------------

SICUREZZA E DATI

- Nessun accesso a Internet
- Nessuna installazione sul sistema
- Dati locali e sotto controllo
- Backup automatici in data\backups

------------------------------------------------------------

SUPPORTO

Per assistenza o chiarimenti,
contattare il referente IT o il fornitore
dell’applicazione.
