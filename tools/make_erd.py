"""Generates the RaceDay ERD as SVG (crow's foot notation). Rendered to PNG with headless Chrome."""
import sys
out = sys.argv[1]

W, H = 1720, 800
HEAD, ROW, BW = 36, 27, 320
NAVY, INK, MUTED, LINE, NEW = "#1f3864", "#1b1f24", "#5b6470", "#3d4652", "#0f7b4f"

# (key, column, type, flag) ; flag: "new" / "changed" / None
entities = {
    "Roles": (40, 110, [
        ("PK", "RoleID", "INT", None),
        ("UQ", "RoleName", "NVARCHAR(50)", None)]),
    "Users": (420, 110, [
        ("PK", "UserID", "INT", None),
        ("FK", "RoleID", "INT", None),
        ("", "FullName", "NVARCHAR(100)", None),
        ("UQ", "Email", "NVARCHAR(150)", None),
        ("", "PasswordHash", "NVARCHAR(255)", None),
        ("", "PhoneNumber", "NVARCHAR(20) NULL", None),
        ("", "CreatedAt", "DATETIME", None)]),
    "Events": (860, 110, [
        ("PK", "EventID", "INT", None),
        ("FK", "OrganiserID", "INT", None),
        ("", "EventName", "NVARCHAR(150)", None),
        ("", "EventDate", "DATE", None),
        ("", "Location", "NVARCHAR(200)", None),
        ("", "Description", "NVARCHAR(MAX) NULL", None),
        ("", "DistanceKm", "DECIMAL(6,2)", "new"),
        ("CK", "EventType", "NVARCHAR(10)", "new"),
        ("", "CreatedAt", "DATETIME", None)]),
    "Categories": (1340, 110, [
        ("PK", "CategoryID", "INT", None),
        ("FK", "EventID", "INT", None),
        ("", "CategoryName", "NVARCHAR(100)", None),
        ("", "DistanceKm", "DECIMAL(5,2) NULL", "changed"),
        ("", "MinAge", "INT NULL", "new"),
        ("", "MaxAge", "INT NULL", "new"),
        ("", "MaxParticipants", "INT", None),
        ("", "EntryFee", "DECIMAL(8,2)", None)]),
    "EventEnrolments": (1340, 500, [
        ("PK", "EnrolmentID", "INT", None),
        ("FK", "ParticipantID", "INT", None),
        ("FK", "CategoryID", "INT", None),
        ("", "BibNumber", "NVARCHAR(10)", None),
        ("", "EnrolmentDate", "DATETIME", None),
        ("", "Status", "NVARCHAR(20)", None)]),
    "Results": (860, 500, [
        ("PK", "ResultID", "INT", None),
        ("FK, UQ", "EnrolmentID", "INT", None),
        ("", "FinishTime", "TIME NULL", None),
        ("", "Position", "INT NULL", None),
        ("", "Status", "NVARCHAR(20)", None)]),
}
notes = {"EventEnrolments": "UQ (ParticipantID, CategoryID)", "Events": "CK: EventType IN ('Run','Walk','Cycle')"}

WIDTH = {"Roles": 250}
def width_of(name): return WIDTH.get(name, 320)

def row_y(name, col):
    x, y, cols = entities[name]
    i = [c[1] for c in cols].index(col)
    return y + HEAD + i * ROW + ROW / 2

def box(name):
    x, y, cols = entities[name]
    return x, y, width_of(name), HEAD + len(cols) * ROW

svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Segoe UI, Helvetica, Arial, sans-serif">',
       f'<rect width="{W}" height="{H}" fill="#ffffff"/>',
       f'<text x="40" y="52" font-size="26" font-weight="700" fill="{INK}">RaceDay – Entity Relationship Diagram</text>',
       f'<text x="40" y="80" font-size="14" fill="{MUTED}">Version 2 · matches docs/RaceDay_Database_Schema.sql and the EF Core InitialCreate migration · crow\'s foot notation</text>']

# ---- relationship lines (drawn first so boxes sit on top) ----
def end_mark(x, y, dx, many, optional):
    """Decoration at an entity edge. (x,y) on the edge, dx = +1 if the line leaves to the right, -1 to the left;
    vertical if dx is a tuple (0, dy)."""
    g = []
    if isinstance(dx, tuple):
        dy = dx[1]
        def P(a, b): return (x + b, y + dy * a)  # a along the line, b across
    else:
        def P(a, b): return (x + dx * a, y + b)
    def ln(p, q): g.append(f'<line x1="{p[0]:.1f}" y1="{p[1]:.1f}" x2="{q[0]:.1f}" y2="{q[1]:.1f}" stroke="{LINE}" stroke-width="1.8"/>')
    if many:
        ln(P(0, -9), P(14, 0)); ln(P(0, 9), P(14, 0)); ln(P(0, 0), P(14, 0))
        inner = 20
    else:
        ln(P(6, -9), P(6, 9))
        inner = 12
    if optional:
        cx, cy = P(inner + 6, 0)
        g.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="5.5" fill="#fff" stroke="{LINE}" stroke-width="1.8"/>')
    else:
        ln(P(inner, -9), P(inner, 9))
    return g

def label(x, y, text, anchor="middle"):
    return [f'<rect x="{x - (len(text) * 3.6 if anchor=="middle" else 0) - 5:.0f}" y="{y - 13}" width="{len(text) * 7.2 + 10:.0f}" height="19" rx="4" fill="#fff"/>',
            f'<text x="{x}" y="{y + 1}" font-size="13" fill="{MUTED}" text-anchor="{anchor}">{text}</text>']

def path(points):
    d = "M " + " L ".join(f"{px:.1f} {py:.1f}" for px, py in points)
    return f'<path d="{d}" fill="none" stroke="{LINE}" stroke-width="1.8"/>'

# Roles 1 -- * Users
x1 = 40 + width_of("Roles"); y1 = row_y("Roles", "RoleID"); x2 = 420; y2 = row_y("Users", "RoleID"); mx = (x1 + x2) / 2
svg += [path([(x1, y1), (mx, y1), (mx, y2), (x2, y2)])]
svg += end_mark(x1, y1, +1, many=False, optional=False) + end_mark(x2, y2, -1, many=True, optional=False)
svg += label(mx, y2 + 30, "1 role : many users")

# Users 1 -- 0..* Events (organises)
x1 = 420 + BW; y1 = row_y("Users", "UserID"); x2 = 860; y2 = row_y("Events", "OrganiserID"); mx = (x1 + x2) / 2
svg += [path([(x1, y1), (mx, y1), (mx, y2), (x2, y2)])]
svg += end_mark(x1, y1, +1, many=False, optional=False) + end_mark(x2, y2, -1, many=True, optional=True)
svg += label(mx, y1 - 14, "organises (1 : 0..*)")

# Events 1 -- 0..* Categories
x1 = 860 + BW; y1 = row_y("Events", "EventID"); x2 = 1340; y2 = row_y("Categories", "EventID"); mx = (x1 + x2) / 2
svg += [path([(x1, y1), (mx, y1), (mx, y2), (x2, y2)])]
svg += end_mark(x1, y1, +1, many=False, optional=False) + end_mark(x2, y2, -1, many=True, optional=True)
svg += label(mx, y1 - 14, "has (1 : 0..*)")

# Categories 1 -- 0..* EventEnrolments (vertical)
bx, by, bw, bh = box("Categories"); ex, ey, ew, eh = box("EventEnrolments")
vx = bx + bw / 2
svg += [path([(vx, by + bh), (vx, ey)])]
svg += end_mark(vx, by + bh, (0, +1), many=False, optional=False) + end_mark(vx, ey, (0, -1), many=True, optional=True)
svg += label(vx + 14, (by + bh + ey) / 2 + 4, "entered via (1 : 0..*)", anchor="start")

# Users 1 -- 0..* EventEnrolments (participant): down from Users, along the bottom, into Enrolments from below-left
ux, uy, uw, uh = box("Users")
sx = ux + uw / 2; ty = ey + eh + 50; tx = ex + 60
svg += [path([(sx, uy + uh), (sx, ty), (tx, ty), (tx, ey + eh)])]
svg += end_mark(sx, uy + uh, (0, +1), many=False, optional=False) + end_mark(tx, ey + eh, (0, +1), many=True, optional=True)
svg += label((sx + tx) / 2, ty + 4, "participates in (1 : 0..*)")

# EventEnrolments 1 -- 0..1 Results
x1 = 1340; y1 = row_y("EventEnrolments", "EnrolmentID"); x2 = 860 + BW; y2 = row_y("Results", "EnrolmentID"); mx = (x1 + x2) / 2
svg += [path([(x1, y1), (mx, y1), (mx, y2), (x2, y2)])]
svg += end_mark(x1, y1, -1, many=False, optional=False) + end_mark(x2, y2, +1, many=False, optional=True)
svg += label(mx, min(y1, y2) - 14, "1 : 0..1")

# ---- entity boxes ----
for name, (x, y, cols) in entities.items():
    BW = width_of(name)
    h = HEAD + len(cols) * ROW
    svg.append(f'<rect x="{x}" y="{y}" width="{BW}" height="{h}" rx="6" fill="#fff" stroke="{NAVY}" stroke-width="1.6"/>')
    svg.append(f'<path d="M {x} {y + HEAD} V {y + 6} Q {x} {y} {x + 6} {y} H {x + BW - 6} Q {x + BW} {y} {x + BW} {y + 6} V {y + HEAD} Z" fill="{NAVY}"/>')
    svg.append(f'<text x="{x + BW / 2}" y="{y + 24}" font-size="16" font-weight="700" fill="#fff" text-anchor="middle">{name}</text>')
    for i, (key, col, typ, flag) in enumerate(cols):
        ry = y + HEAD + i * ROW
        if i % 2 == 1:
            svg.append(f'<rect x="{x + 1}" y="{ry}" width="{BW - 2}" height="{ROW}" fill="#f3f5f8"/>')
        colour = NEW if flag else INK
        svg.append(f'<text x="{x + 10}" y="{ry + 18}" font-size="12" font-weight="700" fill="{NAVY}">{key}</text>')
        weight = "700" if key.startswith("PK") else "400"
        svg.append(f'<text x="{x + 66}" y="{ry + 18}" font-size="13.5" font-weight="{weight}" fill="{colour}">{col}</text>')
        svg.append(f'<text x="{x + BW - 10}" y="{ry + 18}" font-size="12" fill="{colour if flag else MUTED}" text-anchor="end">{typ}</text>')
        if flag:
            svg.append(f'<circle cx="{x + BW + 0.5}" cy="{ry + ROW / 2}" r="4" fill="{NEW}"/>')
    if name in notes:
        svg.append(f'<text x="{x + BW}" y="{y + h + 18}" font-size="12" fill="{MUTED}" text-anchor="end">{notes[name]}</text>')

# ---- legend ----
lx, ly = 40, 470
svg.append(f'<rect x="{lx}" y="{ly}" width="330" height="230" rx="6" fill="#f8f9fb" stroke="#d5dae1"/>')
svg.append(f'<text x="{lx + 16}" y="{ly + 28}" font-size="15" font-weight="700" fill="{INK}">Legend</text>')
items = [((False, False), "exactly one"), ((False, True), "zero or one"), ((True, True), "zero or many"), ((True, False), "one or many")]
for i, ((many, opt), text) in enumerate(items):
    yy = ly + 56 + i * 30
    svg.append(f'<line x1="{lx + 16}" y1="{yy}" x2="{lx + 76}" y2="{yy}" stroke="{LINE}" stroke-width="1.8"/>')
    svg += end_mark(lx + 76, yy, -1, many=many, optional=opt)
    svg.append(f'<text x="{lx + 92}" y="{yy + 5}" font-size="13" fill="{INK}">{text}</text>')
yy = ly + 56 + 4 * 30
svg.append(f'<text x="{lx + 16}" y="{yy + 5}" font-size="12" fill="{MUTED}">PK primary key · FK foreign key · UQ unique · CK check</text>')
svg.append(f'<circle cx="{lx + 22}" cy="{yy + 26}" r="4" fill="{NEW}"/>')
svg.append(f'<text x="{lx + 34}" y="{yy + 31}" font-size="12" fill="{NEW}">added or changed in v2 (see README)</text>')

svg.append('</svg>')
open(out, "w").write("\n".join(svg))
