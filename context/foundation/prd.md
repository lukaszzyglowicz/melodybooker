---
project: "MelodyBooker"
version: 1
status: draft
created: 2026-10-02
context_type: greenfield
product_type: web-app
target_scale:
  users: medium
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 3
  hard_deadline: 2026-12-10
  after_hours_only: true
---

## Vision & Problem Statement

Nauczyciele muzyki w szkole muzycznej planują rezerwacje sal na indywidualne lekcje ręcznie w Excelu. Ręczne planowanie nie wymusza reguły, że sale wyposażone w nieprzenośne instrumenty (fortepian, ksylofon, perkusja) powinny mieć pierwszeństwo rezerwacji dla uczniów grających na tych właśnie instrumentach — podczas gdy uczniowie grający na przenośnych instrumentach (np. skrzypce, altówka, trąbka) mogą zarezerwować dowolną zwykłą salę, bo mogą przynieść instrument ze sobą. Brak tej reguły w arkuszu prowadzi do konfliktów rezerwacji i czasu traconego na ręczną koordynację.

Rozwiązaniem musi być scyfrowana, dostępna dla nauczycieli aplikacja webowa, która sama egzekwuje priorytet rezerwacji sal ze specjalistycznym, nieprzenośnym sprzętem dla uczniów, którzy go potrzebują — to reguła domenowa, której generyczny arkusz kalkulacyjny lub zwykły kalendarz nie wymusza.

## User & Persona

**Nauczyciel muzyki** — posiada konto w systemie (login), ma przypisanych uczniów (każdy uczeń gra na jednym instrumencie i ma jednego nauczyciela oraz 2 lekcje tygodniowo przez cały rok szkolny). Dokonuje rezerwacji sali dla swoich lekcji, kierując się dostępnością sal oraz priorytetem sal ze specjalistycznym instrumentem dla uczniów, którzy go potrzebują. Działa w ramach jednej, konkretnej szkoły muzycznej (nie multi-tenant — rozwiązanie nie musi obsługiwać wielu niezależnych szkół w MVP).

## Success Criteria

### Primary
- Administrator loguje się, dodaje uczniów i nauczycieli, oraz przypisuje każdego ucznia do jednego nauczyciela (instrument, klasa, 2 lekcje/tydzień).
- Nauczyciel loguje się, widzi przypisanych mu uczniów i rezerwuje salę na stały dzień tygodnia/godzinę (powtarzane co tydzień przez cały rok szkolny); czas trwania (20 min dla klas 1–4, 40 min dla klas 5–8) ustalany automatycznie na podstawie klasy ucznia.
- System ostrzega (soft enforcement) nauczyciela, gdy rezerwuje salę ze specjalistycznym, nieprzenośnym instrumentem (fortepian/ksylofon/perkusja) dla ucznia, który go nie potrzebuje.
- Administrator ma podgląd i możliwość edycji wszystkich rezerwacji.

### Secondary
- Twarda blokada (zamiast tylko ostrzeżenia) rezerwacji sal specjalistycznych dla niewłaściwych uczniów.

### Guardrails
- Nigdy nie dochodzi do podwójnej rezerwacji tej samej sali w tym samym terminie.
- Nauczyciel widzi i edytuje wyłącznie rezerwacje swoich przypisanych uczniów.

## User Stories

### US-01: Nauczyciel rezerwuje salę dla zwykłej lekcji

- **Given** zalogowany nauczyciel z przypisanym uczniem grającym na skrzypcach (klasa 3)
- **When** nauczyciel wybiera dostępny termin w zwykłej sali na poniedziałek 10:00
- **Then** rezerwacja zostaje zapisana jako powtarzająca się co tydzień przez cały rok szkolny, z czasem trwania 20 minut, bez żadnego ostrzeżenia

#### Acceptance Criteria
- Czas trwania lekcji jest wyliczany automatycznie z klasy ucznia (20 min dla klas 1–4).
- Termin powtarza się co tydzień aż do końca roku szkolnego bez ręcznego powtarzania przez nauczyciela.
- Próba rezerwacji zajętego już terminu w tej samej sali jest zablokowana z czytelnym komunikatem.

### US-02: System ostrzega przy niewłaściwej rezerwacji sali specjalistycznej

- **Given** zalogowany nauczyciel z przypisanym uczniem grającym na skrzypcach (nie fortepian)
- **When** nauczyciel próbuje zarezerwować dla tego ucznia salę z fortepianem
- **Then** system wyświetla ostrzeżenie o niezgodności instrumentu, ale pozwala nauczycielowi dokończyć rezerwację, jeśli świadomie ją potwierdzi

#### Acceptance Criteria
- Ostrzeżenie pojawia się przed finalnym zatwierdzeniem rezerwacji, nie po fakcie.
- Rezerwacja dla ucznia grającego na fortepianie/ksylofonie/perkusji w sali z tym instrumentem nie wywołuje ostrzeżenia.

## Functional Requirements

### Uwierzytelnianie
- FR-001: Administrator can log in to the system. Priority: must-have
  > Socrates: Counter-argument considered: none raised. Resolution: kept as written.
- FR-002: Teacher can log in to the system. Priority: must-have
  > Socrates: Counter-argument considered: none raised. Resolution: kept as written.

### Panel administratora — zarządzanie użytkownikami
- FR-003: Administrator can add a student (name, class 1–8, instrument). Priority: must-have
  > Socrates: Counter-argument considered: "bulk import from existing Excel instead of one-by-one." Resolution: one-by-one add is sufficient for MVP; bulk import deferred.
- FR-004: Administrator can add a teacher. Priority: must-have
  > Socrates: Counter-argument considered: none raised. Resolution: kept as written.
- FR-005: Administrator can assign a student to exactly one teacher. Priority: must-have
  > Socrates: Counter-argument considered: "a student might eventually need two teachers for two instruments." Resolution: kept; one instrument/one teacher per student matches stated assumptions.
- FR-006: Administrator can edit/remove students and teachers, with explicit handling of existing assignments and reservations when a teacher or student is removed (reassign or block deletion while active reservations exist). Priority: must-have
  > Socrates: Counter-argument considered: "deleting a teacher/student with active assignments/reservations orphans data." Resolution: FR updated to require explicit reassignment or a blocking warning before deletion, rather than silently orphaning records.

### Rezerwacje sal
- FR-007: Teacher can view the list of students assigned to them. Priority: must-have
  > Socrates: Counter-argument considered: "teacher may need schedule-change history, not just the current list." Resolution: current list is sufficient for MVP; history deferred.
- FR-008: Teacher can reserve a room for a student's weekly lesson (day of week + time), repeated for the whole school year. Priority: must-have
  > Socrates: Counter-argument considered: none raised (school holiday handling flagged as an open question, not a counter-argument to the FR itself). Resolution: kept as written; holiday handling tracked in Open Questions.
- FR-009: System automatically sets lesson duration (20 min for classes 1–4, 40 min for classes 5–8) based on the student's class. Priority: must-have
  > Socrates: Counter-argument considered: none raised. Resolution: kept as written.
- FR-010: System blocks double-booking of the same room at the same time slot, with no manual override. Priority: must-have
  > Socrates: Counter-argument considered: "should allow a manual override for exceptional cases." Resolution: rejected — the block must always be strict, no exceptions.
- FR-011: System warns the teacher (does not block) when booking a specialist-instrument room (piano/xylophone/drums) for a student who does not play that instrument. Priority: must-have
  > Socrates: Counter-argument considered: "a single warning is too weak; should require double confirmation." Resolution: kept; a single warning matches the soft-enforcement MVP decision.
- FR-012: System hard-blocks (instead of warning) specialist-room bookings for mismatched students. Priority: nice-to-have
  > Socrates: Counter-argument considered: "since this is v2 scope, remove it from the PRD entirely." Resolution: kept as a documented nice-to-have so the v2 roadmap intent stays visible.

### Nadzór administratora
- FR-013: Administrator can view all room reservations. Priority: must-have
- FR-014: Administrator can edit or cancel any reservation. Priority: must-have
- FR-015: Administrator can filter/search reservations (e.g. by teacher, room, date). Priority: nice-to-have
  > Socrates (FR-013): Counter-argument considered: "admin needs filtering/search, not just a raw list." Resolution: split into a dedicated nice-to-have FR-015 rather than inflating FR-013's MVP scope.
- FR-016: System notifies the affected teacher when an administrator edits or cancels their reservation. Priority: nice-to-have
  > Socrates (FR-014): Counter-argument considered: "admin edits should notify the teacher to avoid silent schedule changes." Resolution: valid concern, but notifications add scope beyond the 3-week MVP; captured as nice-to-have FR-016.

## Non-Functional Requirements

- Dane uczniów (osób niepełnoletnich) są dostępne wyłącznie dla administratora oraz nauczyciela, do którego dany uczeń jest przypisany — żadna inna rola ani nieuwierzytelniony użytkownik nie ma do nich dostępu.
- Aplikacja jest wewnętrznym narzędziem szkolnym bez twardych wymagań wydajnościowych; krótkotrwała niedostępność jest akceptowalna, pod warunkiem że dane rezerwacji nigdy nie są tracone ani niespójne.

## Business Logic

Sala wyposażona w nieprzenośny instrument specjalistyczny (fortepian, ksylofon lub perkusja) powinna być zarezerwowana w pierwszej kolejności dla ucznia, który gra na tym właśnie instrumencie — rezerwacja takiej sali dla innego ucznia jest dozwolona, ale system ostrzega o tym niedopasowaniu.

Reguła konsumuje jako dane wejściowe instrument przypisany do ucznia oraz wyposażenie wybranej sali (czy jest to sala specjalistyczna z fortepianem/ksylofonem/perkusją, czy sala zwykła). Wynikiem jest binarna decyzja widoczna w formularzu rezerwacji: brak ostrzeżenia przy zgodności instrumentu z salą, albo ostrzeżenie przy niedopasowaniu — rezerwacja pozostaje możliwa mimo ostrzeżenia (soft enforcement w v1). Nauczyciel napotyka tę regułę w momencie wyboru sali i ucznia, przed ostatecznym zatwierdzeniem terminu.

## Access Control

Logowanie (konto) dla dwóch ról — Administrator i Nauczyciel. Brak publicznego dostępu bez logowania; próba wejścia niezalogowanego użytkownika przekierowuje do ekranu logowania. Uczniowie nie mają własnego konta ani logowania — ich harmonogram lekcji jest widoczny wyłącznie w panelach Administratora i Nauczyciela.

Role → możliwości:
- **Administrator**: dodaje i edytuje uczniów oraz nauczycieli, przypisuje uczniów do nauczycieli, ma podgląd i możliwość edycji WSZYSTKICH rezerwacji sal.
- **Nauczyciel**: loguje się, może rezerwować salę WYŁĄCZNIE dla przypisanych mu uczniów, widzi harmonogram swoich własnych lekcji.

Uwaga: zdanie z oryginalnych notatek o "tablicach ogłoszeniowych" i publicznej ofercie zostało uznane przez użytkownika za fragment niezwiązany z tym projektem i pominięte.

## Non-Goals

- Brak aplikacji mobilnej — explicit non-goal z notatek wejściowych; tylko aplikacja webowa.
- Brak aplikacji desktopowej — explicit non-goal z notatek wejściowych; tylko aplikacja webowa.
- Brak obsługi wielu szkół / multi-tenant — rozwiązanie służy jednej, konkretnej szkole muzycznej.
- Brak własnego algorytmu optymalizacji przydziału sal (np. automatyczne sugerowanie najlepszej sali) — nauczyciel wybiera salę ręcznie; system tylko ostrzega o niedopasowaniu instrumentu.
- Brak masowego importu uczniów/nauczycieli z Excela — dodawanie odbywa się pojedynczo przez administratora (patrz FR-003).
- Brak powiadomień email/SMS w MVP poza udokumentowanym nice-to-have FR-016 — powiadomienia nie wchodzą w zakres pierwszej wersji.
- Brak logowania/konta dla uczniów — uczniowie nie mają własnego dostępu do systemu.
- Brak funkcji płatności/rozliczeń za lekcje.
- Brak automatycznej obsługi ferii/przerw świątecznych w harmonogramie — wyjątki od cotygodniowego planu obsługuje ręcznie nauczyciel (patrz Open Questions).
- Brak formalnej certyfikacji dostępności (WCAG-AA) w MVP.

## Open Questions

1. **Jak dokładnie obsłużyć ferie/przerwy świąteczne w cotygodniowym harmonogramie rezerwacji na cały rok szkolny?** — Non-goal ustala, że obsługa jest ręczna (nauczyciel sam anuluje/przesuwa pojedyncze terminy), ale dokładny mechanizm UI do tego nie został sprecyzowany. Owner: user. By: przed planowaniem implementacji.
