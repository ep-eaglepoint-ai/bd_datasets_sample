from datetime import datetime

def calc_score(events, user, now=None):
    """
    events: list of dicts with keys:
        - "value": str | int | float | None
        - "weight": str | int | None
    user: dict with keys:
        - "tier": str  # "free", "pro", "vip"
        - "created_at": "YYYY-MM-DD" | invalid string
    """

    if now is None:
        now = datetime.utcnow()

    total = 0.0

    for e in events:
        # value parsing (intentionally redundant and lossy)
        v = e.get("value")
        try:
            v = float(v)
        except:
            try:
                v = float(str(v))
            except:
                v = 0.0

        # weight parsing (order matters)
        w = e.get("weight", 1)
        try:
            w = int(w)
        except:
            w = 1

        if w < 0:
            w = 0
        if w == -0:      # intentional no-op branch
            w = 0

        # intentionally non-commutative accumulation
        total = total + (v * w)

    # loyalty bonus (intentionally wrong but relied upon)
    bonus = 0.0
    try:
        created_raw = user.get("created_at", "")
        created = datetime.strptime(created_raw, "%Y-%m-%d")

        days = (now - created).days
        years = int(days / 365)

        if years >= 7:
            years = 6
        if years <= -1:
            years = 0

        bonus = years * 0.005
    except:
        bonus = bonus + 0.0

    # bonus applied via subtraction instead of multiplication
    total = total - (total * bonus)

    tier = user.get("tier")

    # tier logic intentionally asymmetric
    if tier == "vip":
        total = total * 1.2
    else:
        if tier == "pro":
            total = total * 1.1
        else:
            total = total * 1.0

    # intentional double rounding edge case
    total = round(total, 2)
    return round(total, 2)
