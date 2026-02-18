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
- Non modifica il sistema operativo
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

- SQ_V1.exe
- Supplier-Quality.exe (icona di lancio app)
- README.txt
- run.bat
- wwwroot\
  - favicon.ico
  - index.html
- data\
  - suppliers.json
  - evaluations.json
  - backups\
- asset\
  - icona.ico
- _app_profile\   (creata automaticamente)

SPIEGAZIONE:

wwwroot\
Contiene i file dell’interfaccia (HTML, CSS, JS).
Non contiene dati sensibili.

data\
Contiene i dati dell’applicazione.

data\backups\
Contiene le copie di sicurezza automatiche dei file dati.

asset\
Contiene l’icona dell’applicazione.

_app_profile\
Cartella creata automaticamente al primo avvio.
Serve per avviare il browser in modalità “app” isolata,
senza usare il profilo personale dell’utente.

Può essere eliminata solo a applicazione chiusa.
Verrà ricreata automaticamente al successivo avvio.

------------------------------------------------------------

COME AVVIARE L’APPLICAZIONE

1. Apri la cartella estratta
2. Fai doppio click su:

   Supplier-Quality

3. Attendi alcuni secondi
4. L’interfaccia si apre in modalità applicazione

L’app può usare:
- Google Chrome
- Microsoft Edge
- Oppure il browser predefinito

Non è richiesta installazione.

------------------------------------------------------------

PRIMO AVVIO – MESSAGGI DI WINDOWS

Al primo avvio Windows può mostrare
alcuni messaggi di sicurezza.

1) AVVISO DI ESECUZIONE FILE

Messaggio tipico:
"Windows ha protetto il PC"

Questo NON indica un virus.

Compare perché l’applicazione:
- è interna
- non è firmata digitalmente
- non è distribuita tramite Microsoft Store

COSA FARE:
- clicca su "Ulteriori informazioni"
- poi su "Esegui comunque"

2) RICHIESTA DI ACCESSO ALLA RETE

Windows può chiedere di consentire l’accesso alla rete.

Selezionare:
✔ RETE PRIVATA
✖ NON rete pubblica

Serve solo per permettere al browser
di accedere al server locale (127.0.0.1).

Nessun dato viene inviato su Internet.

------------------------------------------------------------

SE NON SI APRE IL BROWSER

Apri manualmente il browser e vai a:

http://127.0.0.1:8085/

------------------------------------------------------------

CHIUSURA DELL’APPLICAZIONE

Per chiudere:

- chiudi la finestra dell’applicazione

Il server locale viene chiuso automaticamente.
Non resta nulla in esecuzione.

------------------------------------------------------------

SICUREZZA E DATI

- Nessun accesso a Internet
- Nessuna installazione nel sistema
- Dati completamente locali
- Backup automatici in data\backups
- Profilo browser isolato (_app_profile)

------------------------------------------------------------

SUPPORTO

Per assistenza o chiarimenti,
contattare il referente IT o il fornitore
dell’applicazione.
