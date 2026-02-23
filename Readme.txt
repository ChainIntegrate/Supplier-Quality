SUPPLIER QUALITY
Applicazione interna – Avvio rapido

------------------------------------------------------------
COME AVVIARE L’APPLICAZIONE
------------------------------------------------------------

1) Apri la cartella estratta
2) Fai doppio click su:

   Supplier-Quality.exe

3) Attendi alcuni secondi
4) L’interfaccia si apre automaticamente

L’applicazione utilizza:

- Microsoft Edge
- Modalità APP (senza barra indirizzi)
- Modalità GUEST (profilo temporaneo, nessuna sincronizzazione)

Non viene utilizzato il profilo personale del browser.
Non è richiesta installazione.

------------------------------------------------------------
SE NON SI APRE IL BROWSER
------------------------------------------------------------

Apri manualmente Microsoft Edge e vai a:

http://127.0.0.1:8085/

------------------------------------------------------------
CHIUSURA DELL’APPLICAZIONE
------------------------------------------------------------

Metodo consigliato:
- Usa il pulsante "Chiudi" nell’interfaccia

Oppure:
- Esegui stop.bat

Questo arresta il server locale in modo sicuro.

------------------------------------------------------------
PRIMO AVVIO – MESSAGGI DI WINDOWS
------------------------------------------------------------

Al primo avvio Windows può mostrare alcuni messaggi di sicurezza.

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
STRUTTURA DELLA CARTELLA
------------------------------------------------------------

All’interno della cartella troverai:

SQ_V2.exe
Supplier-Quality.exe
run.bat
stop.bat
README.txt

wwwroot\
  favicon.ico
  index.html

data\
  suppliers.json
  evaluations.json
  backups\

develop\
  Program.cs
  ProgramV2.0.0.cs
  ProgramV3.0.0.cs

asset\
  icona.ico
  logo.png

------------------------------------------------------------
FILE CREATI AUTOMATICAMENTE ALL’AVVIO
------------------------------------------------------------

_server.log
_server_pid.txt
_edge_pid.txt

Questi file vengono generati automaticamente
all’avvio dell’applicazione.

Possono essere eliminati solo a applicazione chiusa.

------------------------------------------------------------
SPIEGAZIONE DELLE CARTELLE
------------------------------------------------------------

wwwroot\
Contiene i file dell’interfaccia (HTML, CSS, JS).
Non contiene dati sensibili.

data\
Contiene i dati dell’applicazione.

data\backups\
Contiene le copie di sicurezza automatiche dei file dati.

develop\
Contiene il codice sorgente utilizzato per generare l’eseguibile.

asset\
Contiene l’icona dell’applicazione.

------------------------------------------------------------
SICUREZZA E DATI
------------------------------------------------------------

- Nessun accesso a Internet
- Nessuna installazione nel sistema
- Dati completamente locali
- Backup automatici in data\backups
- Browser avviato in modalità Guest
- Nessuna sincronizzazione attiva

------------------------------------------------------------
SUPPORTO
------------------------------------------------------------

Per assistenza o chiarimenti,
contattare il referente IT o il fornitore dell’applicazione.
