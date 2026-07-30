# TrackNTrash — Client Walkthrough

For the people who will use this, not build it. No code, no Azure.

---

## 1. The problem this solves

A tray leaves your warehouse. Three days later a store says two cartons never arrived. Right
now you cannot prove otherwise — the paperwork says the tray was loaded, and after that there
is nothing. So you credit the store, absorb the loss, and never find out whether the cartons
were mispicked, left on the dock, put on the wrong van, or taken.

TrackNTrash puts a scan at four points in that journey and keeps every one of them forever.
When a store queries a delivery you can answer in seconds, from the record, rather than
arguing from an absence of evidence.

The important part is not the tracking. It is that **discrepancies surface at the checkpoint
where they happened**, while the tray is still in front of someone who can fix it — instead of
three days later in a credit note.

---

## 2. The four checkpoints

| # | Where | Who | The question it answers |
|---|---|---|---|
| 1 | Pick face | Picker | Did the right cartons go into this tray? |
| 2 | Dispatch dock | Camera (automatic) | Does the tray leaving hold what the manifest says? |
| 3 | Vehicle | Driver | Did this tray go onto the correct van? |
| 4 | Store back door | Store colleague | Did what arrived match what was promised? |

Between them an order line moves through six states:

**Ordered → Picked → Staged → Loaded → In Transit → Received**

Anyone in the office can see which of those six any line is sitting in, right now.

---

## 3. A normal day, step by step

### The picker — checkpoint 1

Opens the **Pick app** on a handheld and signs in once. That's it for the shift.

They pick an order, scan the tray label, then scan each carton as it goes in. The app counts
as they go: *3 / 3 scanned*. A carton scanned twice is refused — the app says *Already
scanned* rather than quietly counting it again, because a double-count at the pick face becomes
a phantom shortage at the store.

If the cartons hold individual units that need counting, the picker registers the carton and
scans the units. Units without a barcode are counted visually instead — you type the number a
camera or the picker saw. The system reconciles both signals and takes the higher, because a
scan proves identity while a camera sees units that were never labelled. When the two disagree
it says so, since that usually means an unreadable or unlabelled unit.

Tap **Complete tray build**. The line becomes **Picked**.

### The dock camera — checkpoint 2

No one does anything. A tray slides into the staging zone, a camera overhead takes a short
burst of frames, counts the cartons, and reads the tray label.

Matches the manifest → the line moves to **Staged** and nobody is interrupted.

Does not match → an exception appears on the office board immediately, with the annotated
photo attached, and the tray does not quietly leave. The photo is the point: a count dispute
with a picture attached ends in a minute.

This checkpoint is optional. You can run the whole system without it and have the dock pass
done by hand — worth doing for the first few weeks while the flow settles.

### The driver — checkpoint 3

Opens the **Driver app**, signs in, and scans each tray as it goes on the van.

Scan a tray belonging to a different trip and the app **refuses it and names the trip it
actually belongs to**. That single behaviour prevents the most expensive error in the chain:
a tray delivered to the wrong store, which costs a return leg, a re-pick, and a store that
stops trusting you.

Once every planned tray is on, the trip locks — nothing more can be added. Tap depart and the
lines become **In Transit**.

On the way back the driver scans empty trays onto the van. Skip this and the trays stay
recorded at the store, so your tray fleet looks smaller than it is and you buy trays you
already own.

### The store — checkpoint 4

Opens the **Receiving app**, scans the tray, and works through the cartons against the ASN —
the list of what should be there.

| What happens | What the colleague sees |
|---|---|
| Expected carton | *Received*, tally increments |
| Same carton twice | *Duplicate* — not counted again |
| Carton not on the list | *Over* — **and which store it actually belongs to** |
| Carton arrived broken | *Damaged* — a photo is required |
| Carton never arrived | listed as short when they complete |

They finish by capturing the receiver's name, and a signature or photo. The lines become
**Received**, and the shortage list is recorded against the delivery — not against a memory of
it three days later.

If the tablet restarts halfway through a tray, the session resumes exactly where it was. The
cartons already scanned stay scanned.

### The office

The **Admin & Exception Console** in a browser. The exception board is live — problems appear
as they are raised, without a refresh.

Each exception can be acknowledged, resolved with a reason, or escalated. Every one of those
actions records who did it and when, and that trail survives restarts. You can see the full
event timeline for any order line: every scan, in order, with times.

---

## 4. When something goes wrong

Eleven problems are recognised by name and ranked, so the board sorts itself and the worst
things rise.

| Severity | Problem | Meaning |
|---|---|---|
| **Critical** | Wrong trip | A tray was scanned onto the wrong van |
| **Critical** | Wrong store | Stock reached the wrong destination |
| **High** | Count mismatch | The dock camera disagrees with the manifest |
| **High** | Missing carton | Expected and never scanned |
| **High** | Unknown carton | Arrived but not on any list for this store |
| **High** | Damaged | Arrived broken; photo attached |
| **High** | Short shipped | Fewer cartons than ordered |
| **High** | No receive within SLA | 24 hours after dispatch, nothing received |
| **Medium** | Suspected lost | Last seen a while ago, nothing since |
| **Medium** | Out-of-order scan | Scans arrived in an impossible sequence |
| **Low** | Tray dwell exceeded | A tray has sat somewhere over 3 days |

Two of these need explaining because they are the ones that build trust in the data.

**Out-of-order scan.** If a device posts something impossible — a store receipt for a tray that
was never loaded — the system **still records the scan** and raises this instead. It never
discards the event. A device with a flat battery, a dead spot in the yard, or a colleague
working out of sequence must never be able to lose data. You get both the event and the flag
that it did not fit.

**No receive within SLA.** Nobody has to notice. A scheduled job looks for anything dispatched
over 24 hours ago and not received, and raises it. Silence is the failure mode this catches —
a delivery nobody chased.

---

## 5. Who sees what

| Role | Can do |
|---|---|
| Picker | Pick app; own tray builds |
| Driver | Driver app; own trips |
| Store manager | Receiving app; own store's deliveries |
| Dispatcher | Console — trips, orders, exception board |
| Warehouse manager | The above plus masters and reporting |
| Admin | Everything, including users and permissions |

Permissions are per role, per screen, and split four ways: view, create, edit, delete. So a
dispatcher can see the tray master without being able to change it.

The dock camera has its own account, and it is **deliberately crippled**. It can do exactly two
things: fetch the manifest list, and report that it is alive. A camera sits unattended in a
warehouse where a contractor can reach it, so we treat its credentials as already stolen. If
they are, they get you nothing — no orders, no trips, no stock movements.

---

## 6. What you need to provide

### To pilot — genuinely almost nothing

One Android phone, a browser, and the master data. The apps also run on Windows, so a laptop
works. Skip the camera at first.

### Handhelds, per person scanning

Android 8.0 or later, 2 GB RAM, and **an autofocus camera** — that last one matters more than
anything else on the spec sheet. A fixed-focus budget handset will not reliably read a carton
QR at arm's length and will make everyone hate the system on day one. Rugged handhelds with a
hardware scan trigger work as-is, no changes needed.

### The dock camera, when you add it

Mounted overhead 2.2–2.8 m up, looking straight down at the staging zone, with even lighting of
at least 500 lux and no glare on the carton tape. 1080p minimum, focus locked after setup.

**One thing to decide early: your carton QR labels must be at least 35 mm.** Smaller and the
camera reads them intermittently, which is worse than not reading them at all — you get
sporadic false shortages that erode confidence. If your labels are already printed smaller,
that is a print change to budget for, and it is cheaper to find out now.

### Master data — the real work

This is where implementation time actually goes:

- Sites, zones, racks and trays
- Stores, with codes matching your ERP
- Products with GTINs
- Vehicles and routes
- People and their roles

The hierarchy is **Site → Zone → Rack → Tray → Carton → Item**, with a store as the
destination. If your operation is shaped differently, say so now rather than after loading.

---

## 7. Rolling it out

| Stage | What happens | Time |
|---|---|---|
| 1 | System stood up, admin access confirmed | ~1 day |
| 2 | Master data loaded and checked | 1–2 days |
| 3 | One tray walked end to end by your own staff | half a day |
| 4 | One store, one van, live, camera off | 1–2 weeks |
| 5 | Review exceptions raised — are they real? | half a day |
| 6 | Dock camera commissioned | 2–3 days per dock |
| 7 | Widen to remaining stores | your pace |

Stage 5 is the one people skip and shouldn't. The first week always produces exceptions that
are really process quirks rather than errors, and every one you leave in place teaches people
to ignore the board. Tune it before widening.

---

## 8. Honest limitations

- **Nothing is tracked between the van leaving and arriving** unless you add vehicle telematics.
  Departure and arrival are recorded; the road in between is not.
- **The camera counts cartons, it does not identify them.** It tells you *four cartons, expected
  five*. Which one is missing comes from the scans.
- **A carton nobody scans is invisible.** The system records what happened at four checkpoints;
  it cannot see a carton that bypassed all of them.
- **Visual unit counting is an estimate.** For unlabelled units the camera's count is a good
  signal, not proof of identity. Where you need certainty, the units need barcodes.
- **The detection model is trained on your cartons.** Accuracy depends on that training and will
  drift as packaging changes. It needs periodic re-validation, which is a real ongoing task, not
  a one-off setup step.

---

## 9. The one-paragraph version

Scan a tray at four points — pick, dock, van, store — and keep every scan forever. Problems
surface at the checkpoint where they happen, while someone can still fix them, instead of
arriving as a credit note three days later. The wrong-van check alone prevents the most
expensive error in the chain. You can pilot it with one phone and a browser; the camera and the
rugged hardware are optimisations you add once the flow is proven.
