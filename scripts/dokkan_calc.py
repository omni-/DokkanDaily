#!/usr/bin/env python3
"""Dokkan unit calculator following Razzer's Calcing Guide (kandymanis/dokkanalytics).

Bracket order (each bracket is multiplicative and floored, per the guide):

    ATK: Base * Lead * Phase1(SoT) * Domain * Items * Links * Actives * Ki * Phase2 * SA
    DEF: Base * Lead * Phase1(SoT) * Domain * Items * Links * Actives * Phase2 * SA Effects

Two different things get called "stacking", and they are modelled separately here:

* Buildup - capped passive accumulation, e.g. "ATK & DEF +20% whenever an attack
  is received (up to 100%)". It lives in the SoT bracket or the mid-battle bracket
  depending on when it applies, and it is what "no buildup" vs "fully built" means.
  Feed it in as the min/max range of the bracket it belongs to.
* SA-effect stacks - uncapped, gained per super, in the final SA bracket. These
  never "finish"; how big they get depends purely on how many supers you have hit.

Conditionals that are not buildup (12+ Ki, attacker slot, HP thresholds) belong in
whichever bracket they apply to, at whatever value the scenario you are calcing has.

Usage:
    python dokkan_calc.py --base-atk 22000 --base-def 15000 \
        --sot-atk-min 200 --sot-atk-max 400 \
        --sot-def-min 200 --sot-def-max 400 \
        --mult-atk 100 --mult-def 100 \
        --additionals 2 --sa-atk-stack 50 --sa-def-stack 30

    python dokkan_calc.py --config unit.json
"""

import argparse
import json
import math
from dataclasses import asdict, dataclass, fields
from typing import Optional

# Base Super Attack multipliers (SA 20), before the Hidden Potential SA Boost node.
# Mirrors getBaseSAMultiplier() in dokkanalytics/script.js.
SA_MULTIPLIERS = {
    ("supreme", "tur", False): 430,
    ("supreme", "tur", True): 530,
    ("immense", "tur", False): 505,
    ("immense", "tur", True): 630,
    ("colossal", "lr", False): 425,
    ("colossal", "lr", True): 450,
    ("mega-colossal", "lr", False): 570,
    ("mega-colossal", "lr", True): 620,
    ("ultimate", "tur", False): 740,
    ("ultimate", "tur", True): 790,
    ("ultimate", "lr", False): 840,
    ("ultimate", "lr", True): 890,
}

HIPO_SA_BOOST_PER_LEVEL = 5


def base_sa_multiplier(sa_type: str, rarity: str, eza: bool) -> float:
    """Base SA multiplier in percent for an SA 20 unit, before Hidden Potential."""
    return SA_MULTIPLIERS.get((sa_type, rarity, eza), 500)


@dataclass
class Unit:
    """Every number is a percentage unless the name says otherwise."""

    base_atk: int = 0
    base_def: int = 0

    # Start of turn passive bracket, floor and ceiling of any SoT buildup.
    sot_atk_min: float = 0.0
    sot_atk_max: float = 0.0
    sot_def_min: float = 0.0
    sot_def_max: float = 0.0

    # Mid-battle (phase 2) bracket. mult_* is the flat multiplicative buff
    # ("when performing a Super Attack" / "when receiving an attack");
    # buildup_* is any capped mid-battle buildup, added on top when fully built.
    mult_atk: float = 0.0
    mult_def: float = 0.0
    buildup_atk: float = 0.0
    buildup_def: float = 0.0

    additionals: int = 0

    sa_type: str = "mega-colossal"
    rarity: str = "lr"
    eza: bool = False
    sa_multiplier: Optional[float] = None  # overrides sa_type/rarity/eza when set
    hipo_sa_boost_level: int = 15
    ki_multiplier: float = 200.0  # 24-Ki on an LR

    # SA effects, uncapped. "flat" is a non-stacking raise that applies on every
    # super including the first; "stack" is the per-super cumulative raise.
    sa_atk_flat: float = 0.0
    sa_atk_stack: float = 0.0
    sa_def_flat: float = 0.0
    sa_def_stack: float = 0.0
    # True for a genuine cumulative stack that carries between turns. False for a
    # "for 1 turn" raise, which stacks across supers within a turn but then resets.
    sa_stacks_persist: bool = True

    leader_skill: float = 440.0
    domain_atk: float = 0.0
    domain_def: float = 0.0
    items_atk: float = 0.0
    items_def: float = 0.0
    links_atk: float = 0.0
    links_def: float = 0.0
    active_atk: float = 0.0
    active_def: float = 0.0

    stacking_turns: int = 5
    stacking_includes_hipo_additional: bool = False

    def sa_base(self) -> float:
        """SA multiplier percent including the Hidden Potential SA Boost node."""
        if self.sa_multiplier is not None:
            return self.sa_multiplier
        boost = self.hipo_sa_boost_level * HIPO_SA_BOOST_PER_LEVEL
        return base_sa_multiplier(self.sa_type, self.rarity, self.eza) + boost

    def mid_atk(self, built: bool) -> float:
        """Mid-battle ATK bracket, with mid-battle buildup included when built."""
        return self.mult_atk + (self.buildup_atk if built else 0.0)

    def mid_def(self, built: bool) -> float:
        """Mid-battle DEF bracket, with mid-battle buildup included when built."""
        return self.mult_def + (self.buildup_def if built else 0.0)

    def supers_per_turn(self) -> int:
        """Supers per turn from the passive's additionals (assumes all supers)."""
        return 1 + max(0, self.additionals)

    def max_supers(self) -> int:
        """Supers per turn including the +1 additional from Hidden Potential."""
        return self.supers_per_turn() + 1

    def supers_over_stacking_turns(self) -> int:
        """Total supers across the multi-turn stacking window."""
        per_turn = self.max_supers() if self.stacking_includes_hipo_additional else self.supers_per_turn()
        return per_turn * self.stacking_turns


def _apply(value: float, pct: float) -> int:
    """Apply one additive-percent bracket and floor, as the game does."""
    return math.floor(value * (1 + pct / 100.0))


def atk_on_super(unit: Unit, sot_pct: float, mid_pct: float, sa_effect_pct: float) -> int:
    """ATK on a super attack, floored at every bracket."""
    atk = unit.base_atk
    atk = _apply(atk, unit.leader_skill)
    atk = _apply(atk, sot_pct)
    atk = _apply(atk, unit.domain_atk)
    atk = _apply(atk, unit.items_atk)
    atk = _apply(atk, unit.links_atk)
    atk = _apply(atk, unit.active_atk)
    atk = math.floor(atk * (unit.ki_multiplier / 100.0))
    atk = _apply(atk, mid_pct)
    return math.floor(atk * ((unit.sa_base() + sa_effect_pct) / 100.0))


def defense(unit: Unit, sot_pct: float, mid_pct: float, sa_effect_pct: float) -> int:
    """DEF value, floored at every bracket.

    Pass mid_pct as 0 for the plain start-of-turn value, or as the "when receiving
    an attack" bracket for the DEF the unit actually defends with when it is hit.
    """
    value = unit.base_def
    value = _apply(value, unit.leader_skill)
    value = _apply(value, sot_pct)
    value = _apply(value, unit.domain_def)
    value = _apply(value, unit.items_def)
    value = _apply(value, unit.links_def)
    value = _apply(value, unit.active_def)
    value = _apply(value, mid_pct)
    return _apply(value, sa_effect_pct)


def atk_sa_effect(unit: Unit, supers_already_done: int) -> float:
    """SA-effect ATK percent for the next super after N stacking supers.

    Infinite ATK stackers start at -1 stack, so the Nth stacking super carries
    (N - 1) stacks. Passing N supers already done gives exactly N stacks here.
    """
    return unit.sa_atk_flat + supers_already_done * unit.sa_atk_stack


def atk_sa_effect_on_super(unit: Unit, super_index: int) -> float:
    """SA-effect ATK percent on the Nth super of a turn.

    A cumulative stacker carries the stacker penalty, so its Nth super has N - 1
    stacks. A "for 1 turn" raise applies on the super that grants it, so its Nth
    super has N raises.
    """
    prior = super_index - 1 if unit.sa_stacks_persist else super_index
    return unit.sa_atk_flat + prior * unit.sa_atk_stack


def def_sa_effect(unit: Unit, supers: int) -> float:
    """SA-effect DEF percent after N supers. DEF stacks apply on the super that grants them."""
    if supers <= 0:
        return 0.0
    return unit.sa_def_flat + supers * unit.sa_def_stack


def build_report(unit: Unit) -> dict:
    """All requested metrics for a unit, as raw numbers.

    "Fully built" means the passive buildup is maxed, in both the SoT and the
    mid-battle bracket. SA-effect stacks are uncapped and never finish building,
    so they are reported per super count instead.
    """
    stacking_supers = unit.supers_over_stacking_turns()
    bare_atk, built_atk = unit.mid_atk(False), unit.mid_atk(True)
    bare_def, built_def = unit.mid_def(False), unit.mid_def(True)

    first_sa_effect = atk_sa_effect_on_super(unit, 1)
    first_sa_no_buildup = atk_on_super(unit, unit.sot_atk_min, bare_atk, first_sa_effect)
    first_sa_built = atk_on_super(unit, unit.sot_atk_max, built_atk, first_sa_effect)
    atk_after_supers = [
        atk_on_super(unit, unit.sot_atk_max, built_atk, atk_sa_effect_on_super(unit, n))
        for n in range(1, unit.max_supers() + 1)
    ]
    first_sa_stacked = (
        atk_on_super(unit, unit.sot_atk_max, built_atk, atk_sa_effect(unit, stacking_supers))
        if unit.sa_atk_stack and unit.sa_stacks_persist
        else None
    )

    # SoT DEF is the phase 1 value on its own; the "when receiving an attack"
    # bracket is what the unit actually defends with once it is hit.
    sot_def_no_buildup = defense(unit, unit.sot_def_min, 0.0, 0.0)
    sot_def_built = defense(unit, unit.sot_def_max, 0.0, 0.0)
    recv_def_no_buildup = defense(unit, unit.sot_def_min, bare_def, 0.0)
    recv_def_built = defense(unit, unit.sot_def_max, built_def, 0.0)
    def_after_supers = [
        defense(unit, unit.sot_def_max, built_def, def_sa_effect(unit, n))
        for n in range(1, unit.max_supers() + 1)
    ]
    def_after_stacking = (
        defense(unit, unit.sot_def_max, built_def, def_sa_effect(unit, stacking_supers))
        if unit.sa_def_stack and unit.sa_stacks_persist
        else None
    )

    return {
        "supers_per_turn": unit.supers_per_turn(),
        "max_supers": unit.max_supers(),
        "sa_multiplier_pct": unit.sa_base(),
        "stacking_supers": stacking_supers,
        "first_sa_no_buildup": first_sa_no_buildup,
        "first_sa_fully_built": first_sa_built,
        "first_sa_after_stacking": first_sa_stacked,
        "atk_after_supers": atk_after_supers,
        "sot_def_no_buildup": sot_def_no_buildup,
        "sot_def_fully_built": sot_def_built,
        "recv_def_no_buildup": recv_def_no_buildup,
        "recv_def_fully_built": recv_def_built,
        "def_after_supers": def_after_supers,
        "def_after_stacking": def_after_stacking,
    }


def _fmt(value) -> str:
    if value is None:
        return "n/a"
    return f"{int(value):,}"


LABEL_WIDTH = 42
VALUE_WIDTH = 16


def _row(label: str, value) -> str:
    return f"  {label:<{LABEL_WIDTH}}{_fmt(value):>{VALUE_WIDTH}}"


def format_report(unit: Unit, report: dict) -> str:
    width = LABEL_WIDTH + VALUE_WIDTH + 2
    turns = unit.stacking_turns
    lines = [
        "=" * width,
        f"  Base {unit.base_atk:,} ATK / {unit.base_def:,} DEF"
        f" | lead +{unit.leader_skill:g}%"
        f" | {unit.ki_multiplier:g}% Ki"
        f" | SA {report['sa_multiplier_pct']:g}%",
        f"  {unit.additionals} passive additional(s)"
        f" -> {report['supers_per_turn']} supers/turn,"
        f" {report['max_supers']} with the Hidden Potential additional",
        "=" * width,
        "",
        "ATK",
        _row("First SA, no buildup", report["first_sa_no_buildup"]),
        _row("First SA, fully built", report["first_sa_fully_built"]),
        _row(f"First SA, built + {turns} turns of SA stacks", report["first_sa_after_stacking"]),
        # The 1st SA of the turn is the "fully built" row above, so start at the 2nd.
        *(
            _row(f"{n}{'nd' if n == 2 else 'rd' if n == 3 else 'th'} SA of the turn", value)
            for n, value in enumerate(report["atk_after_supers"][1:], start=2)
        ),
        "",
        "DEF at start of turn (phase 1 only)",
        _row("SoT DEF, no buildup", report["sot_def_no_buildup"]),
        _row("SoT DEF, fully built", report["sot_def_fully_built"]),
        "",
        "DEF when receiving an attack",
        _row("When receiving, min", report["recv_def_no_buildup"]),
        _row("When receiving, max", report["recv_def_fully_built"]),
    ]
    last = report["max_supers"]
    for n, value in enumerate(report["def_after_supers"], start=1):
        suffix = " (incl. HiPo additional)" if n == last else ""
        lines.append(_row(f"After {n} SA{suffix}", value))
    lines.append(
        _row(
            f"After {turns} turns of SA stacks ({report['stacking_supers']} SAs)",
            report["def_after_stacking"],
        )
    )

    notes = []
    if not unit.sa_stacks_persist:
        notes.append(
            "SA raises last 1 turn, so they stack within a turn but reset after it;"
            " the multi-turn rows do not apply"
        )
    else:
        if report["first_sa_after_stacking"] is None:
            notes.append("unit has no stacking SA ATK effect (--sa-atk-stack is 0)")
        if report["def_after_stacking"] is None:
            notes.append("unit has no stacking SA DEF effect (--sa-def-stack is 0)")
    if report["def_after_stacking"] is not None:
        notes.append(
            "the fully built rows use one turn's supers; SA stacks are uncapped,"
            " so the +turns row shows where they land after a longer fight"
        )
    if notes:
        lines.append("")
        lines.extend(f"  Note: {note}." for note in notes)
    return "\n".join(lines)


def parse_args(argv=None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Calculate Dokkan ATK and DEF benchmarks for a unit.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--config", help="JSON file with any of the options below (long names, underscores)")

    stats = parser.add_argument_group("base stats")
    stats.add_argument("--base-atk", type=int)
    stats.add_argument("--base-def", type=int)
    stats.add_argument("--leader-skill", type=float, help="total lead percent, e.g. 440 for dual 220%%")

    sot = parser.add_argument_group("start of turn passive (min = no buildup, max = fully built)")
    sot.add_argument("--sot-atk-min", type=float)
    sot.add_argument("--sot-atk-max", type=float)
    sot.add_argument("--sot-def-min", type=float)
    sot.add_argument("--sot-def-max", type=float)

    mid = parser.add_argument_group("mid-battle bracket")
    mid.add_argument("--mult-atk", type=float, help='ATK %% "when performing a Super Attack"')
    mid.add_argument("--mult-def", type=float, help='DEF %% "when receiving an attack"')
    mid.add_argument(
        "--buildup-atk",
        type=float,
        help="extra mid-battle ATK %% in the max state (buildup at its cap, or a conditional active)",
    )
    mid.add_argument(
        "--buildup-def",
        type=float,
        help="extra mid-battle DEF %% in the max state (buildup at its cap, or a conditional active)",
    )

    sa = parser.add_argument_group("super attack")
    sa.add_argument("--sa-type", choices=["supreme", "immense", "colossal", "mega-colossal", "ultimate"])
    sa.add_argument("--rarity", choices=["tur", "lr"])
    sa.add_argument("--eza", action="store_true", default=None)
    sa.add_argument("--sa-multiplier", type=float, help="override the whole SA multiplier %%")
    sa.add_argument("--hipo-sa-boost-level", type=int, help="SA Boost node level (+5%% each)")
    sa.add_argument("--ki-multiplier", type=float, help="Ki multiplier %%, e.g. 200 for LR 24-Ki")
    sa.add_argument("--sa-atk-flat", type=float, help="non-stacking SA ATK %% (applies on the first super)")
    sa.add_argument("--sa-atk-stack", type=float, help="cumulative SA ATK %% per super")
    sa.add_argument("--sa-def-flat", type=float, help="non-stacking SA DEF %%")
    sa.add_argument("--sa-def-stack", type=float, help="cumulative SA DEF %% per super")
    sa.add_argument(
        "--sa-stacks-reset",
        dest="sa_stacks_persist",
        action="store_false",
        default=None,
        help='SA raise lasts "for 1 turn", so it resets each turn instead of carrying over',
    )

    other = parser.add_argument_group("other brackets")
    other.add_argument("--domain-atk", type=float)
    other.add_argument("--domain-def", type=float)
    other.add_argument("--items-atk", type=float)
    other.add_argument("--items-def", type=float)
    other.add_argument("--links-atk", type=float)
    other.add_argument("--links-def", type=float)
    other.add_argument("--active-atk", type=float)
    other.add_argument("--active-def", type=float)

    supers = parser.add_argument_group("super counts")
    supers.add_argument("--additionals", type=int, help="number of additionals in the passive")
    supers.add_argument("--stacking-turns", type=int)
    supers.add_argument(
        "--stacking-includes-hipo-additional",
        action="store_true",
        default=None,
        help="count the Hidden Potential additional in the multi-turn stacking window",
    )

    parser.add_argument("--json", action="store_true", help="print raw numbers as JSON")
    return parser.parse_args(argv)


def unit_from_args(args: argparse.Namespace) -> Unit:
    values = {}
    if args.config:
        with open(args.config, encoding="utf-8") as handle:
            values.update(json.load(handle))

    known = {f.name for f in fields(Unit)}
    unknown = set(values) - known
    if unknown:
        raise SystemExit(f"Unknown config keys: {', '.join(sorted(unknown))}")

    for name in known:
        supplied = getattr(args, name, None)
        if supplied is not None:
            values[name] = supplied

    return Unit(**values)


def main(argv=None) -> int:
    args = parse_args(argv)
    unit = unit_from_args(args)
    if not unit.base_atk and not unit.base_def:
        raise SystemExit("Nothing to calculate: pass --base-atk / --base-def or a --config file.")

    report = build_report(unit)
    if args.json:
        print(json.dumps({"unit": asdict(unit), "report": report}, indent=2))
    else:
        print(format_report(unit, report))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
