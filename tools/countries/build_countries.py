"""Builds src/Geopolitics.Infrastructure/Countries/countries.json from a saved Worldometers page.

The app never contacts Worldometers. This script is run once, by hand, against a copy of
https://www.worldometers.info/world-population/population-by-country/ saved to disk:

    python -I tools/countries/build_countries.py <saved-page.html> <capture-date YYYY-MM-DD>

Only the 195 sovereign states are kept: the 193 UN member states plus the two UN observer
states (Holy See and State of Palestine). Dependencies and disputed territories are dropped.
The script fails if any of the 195 is missing from the page, so a renamed row is caught.
"""

import html
import json
import re
import sys
from pathlib import Path

SOURCE_URL = "https://www.worldometers.info/world-population/population-by-country/"
OUTPUT = Path(__file__).resolve().parents[2] / "src" / "Geopolitics.Infrastructure" / "Countries" / "countries.json"

# Worldometers row name -> (ISO 3166-1 alpha-2 code, display name if different from the row name).
SOVEREIGN_STATES = {
    "Afghanistan": ("AF", None), "Albania": ("AL", None), "Algeria": ("DZ", None),
    "Andorra": ("AD", None), "Angola": ("AO", None), "Antigua and Barbuda": ("AG", None),
    "Argentina": ("AR", None), "Armenia": ("AM", None), "Australia": ("AU", None),
    "Austria": ("AT", None), "Azerbaijan": ("AZ", None), "Bahamas": ("BS", None),
    "Bahrain": ("BH", None), "Bangladesh": ("BD", None), "Barbados": ("BB", None),
    "Belarus": ("BY", None), "Belgium": ("BE", None), "Belize": ("BZ", None),
    "Benin": ("BJ", None), "Bhutan": ("BT", None), "Bolivia": ("BO", None),
    "Bosnia and Herzegovina": ("BA", None), "Botswana": ("BW", None), "Brazil": ("BR", None),
    "Brunei": ("BN", None), "Bulgaria": ("BG", None), "Burkina Faso": ("BF", None),
    "Burundi": ("BI", None), "Cabo Verde": ("CV", None), "Cambodia": ("KH", None),
    "Cameroon": ("CM", None), "Canada": ("CA", None), "Central African Republic": ("CF", None),
    "Chad": ("TD", None), "Chile": ("CL", None), "China": ("CN", None),
    "Colombia": ("CO", None), "Comoros": ("KM", None), "Congo": ("CG", "Republic of the Congo"),
    "Costa Rica": ("CR", None), "Côte d'Ivoire": ("CI", None), "Croatia": ("HR", None),
    "Cuba": ("CU", None), "Cyprus": ("CY", None), "Czech Republic (Czechia)": ("CZ", "Czechia"),
    "DR Congo": ("CD", None), "Denmark": ("DK", None), "Djibouti": ("DJ", None),
    "Dominica": ("DM", None), "Dominican Republic": ("DO", None), "Ecuador": ("EC", None),
    "Egypt": ("EG", None), "El Salvador": ("SV", None), "Equatorial Guinea": ("GQ", None),
    "Eritrea": ("ER", None), "Estonia": ("EE", None), "Eswatini": ("SZ", None),
    "Ethiopia": ("ET", None), "Fiji": ("FJ", None), "Finland": ("FI", None),
    "France": ("FR", None), "Gabon": ("GA", None), "Gambia": ("GM", None),
    "Georgia": ("GE", None), "Germany": ("DE", None), "Ghana": ("GH", None),
    "Greece": ("GR", None), "Grenada": ("GD", None), "Guatemala": ("GT", None),
    "Guinea": ("GN", None), "Guinea-Bissau": ("GW", None), "Guyana": ("GY", None),
    "Haiti": ("HT", None), "Holy See": ("VA", "Vatican City"), "Honduras": ("HN", None),
    "Hungary": ("HU", None), "Iceland": ("IS", None), "India": ("IN", None),
    "Indonesia": ("ID", None), "Iran": ("IR", None), "Iraq": ("IQ", None),
    "Ireland": ("IE", None), "Israel": ("IL", None), "Italy": ("IT", None),
    "Jamaica": ("JM", None), "Japan": ("JP", None), "Jordan": ("JO", None),
    "Kazakhstan": ("KZ", None), "Kenya": ("KE", None), "Kiribati": ("KI", None),
    "Kuwait": ("KW", None), "Kyrgyzstan": ("KG", None), "Laos": ("LA", None),
    "Latvia": ("LV", None), "Lebanon": ("LB", None), "Lesotho": ("LS", None),
    "Liberia": ("LR", None), "Libya": ("LY", None), "Liechtenstein": ("LI", None),
    "Lithuania": ("LT", None), "Luxembourg": ("LU", None), "Madagascar": ("MG", None),
    "Malawi": ("MW", None), "Malaysia": ("MY", None), "Maldives": ("MV", None),
    "Mali": ("ML", None), "Malta": ("MT", None), "Marshall Islands": ("MH", None),
    "Mauritania": ("MR", None), "Mauritius": ("MU", None), "Mexico": ("MX", None),
    "Micronesia": ("FM", None), "Moldova": ("MD", None), "Monaco": ("MC", None),
    "Mongolia": ("MN", None), "Montenegro": ("ME", None), "Morocco": ("MA", None),
    "Mozambique": ("MZ", None), "Myanmar": ("MM", None), "Namibia": ("NA", None),
    "Nauru": ("NR", None), "Nepal": ("NP", None), "Netherlands": ("NL", None),
    "New Zealand": ("NZ", None), "Nicaragua": ("NI", None), "Niger": ("NE", None),
    "Nigeria": ("NG", None), "North Korea": ("KP", None), "North Macedonia": ("MK", None),
    "Norway": ("NO", None), "Oman": ("OM", None), "Pakistan": ("PK", None),
    "Palau": ("PW", None), "Panama": ("PA", None), "Papua New Guinea": ("PG", None),
    "Paraguay": ("PY", None), "Peru": ("PE", None), "Philippines": ("PH", None),
    "Poland": ("PL", None), "Portugal": ("PT", None), "Qatar": ("QA", None),
    "Romania": ("RO", None), "Russia": ("RU", None), "Rwanda": ("RW", None),
    "Saint Kitts & Nevis": ("KN", "Saint Kitts and Nevis"), "Saint Lucia": ("LC", None),
    "St. Vincent & Grenadines": ("VC", "Saint Vincent and the Grenadines"), "Samoa": ("WS", None),
    "San Marino": ("SM", None), "Sao Tome & Principe": ("ST", "São Tomé and Príncipe"),
    "Saudi Arabia": ("SA", None), "Senegal": ("SN", None), "Serbia": ("RS", None),
    "Seychelles": ("SC", None), "Sierra Leone": ("SL", None), "Singapore": ("SG", None),
    "Slovakia": ("SK", None), "Slovenia": ("SI", None), "Solomon Islands": ("SB", None),
    "Somalia": ("SO", None), "South Africa": ("ZA", None), "South Korea": ("KR", None),
    "South Sudan": ("SS", None), "Spain": ("ES", None), "Sri Lanka": ("LK", None),
    "State of Palestine": ("PS", "Palestine"), "Sudan": ("SD", None), "Suriname": ("SR", None),
    "Sweden": ("SE", None), "Switzerland": ("CH", None), "Syria": ("SY", None),
    "Tajikistan": ("TJ", None), "Tanzania": ("TZ", None), "Thailand": ("TH", None),
    "Timor-Leste": ("TL", None), "Togo": ("TG", None), "Tonga": ("TO", None),
    "Trinidad and Tobago": ("TT", None), "Tunisia": ("TN", None), "Turkey": ("TR", None),
    "Turkmenistan": ("TM", None), "Tuvalu": ("TV", None), "Uganda": ("UG", None),
    "Ukraine": ("UA", None), "United Arab Emirates": ("AE", None), "United Kingdom": ("GB", None),
    "United States": ("US", None), "Uruguay": ("UY", None), "Uzbekistan": ("UZ", None),
    "Vanuatu": ("VU", None), "Venezuela": ("VE", None), "Vietnam": ("VN", None),
    "Yemen": ("YE", None), "Zambia": ("ZM", None), "Zimbabwe": ("ZW", None),
}


def read_rows(page: str) -> dict[str, int]:
    """Country name -> population from the page's population table."""
    body = page[page.index("<tbody>"):page.index("</tbody>")]
    rows = {}
    for row in re.findall(r"<tr[^>]*>(.*?)</tr>", body, re.S):
        cells = [html.unescape(re.sub(r"<[^>]+>", "", c)).strip() for c in re.findall(r"<td[^>]*>(.*?)</td>", row, re.S)]
        rows[cells[1]] = int(cells[2].replace(",", ""))
    return rows


def main() -> None:
    if len(sys.argv) != 3 or not re.fullmatch(r"\d{4}-\d{2}-\d{2}", sys.argv[2]):
        sys.exit(__doc__)
    page_path, captured_on = Path(sys.argv[1]), sys.argv[2]
    page = page_path.read_text(encoding="utf-8")

    year = re.search(r"<span>Population (\d{4})</span>", page)
    rows = read_rows(page)

    missing = sorted(set(SOVEREIGN_STATES) - set(rows))
    if missing:
        sys.exit(f"Not found on the page (renamed?): {missing}")
    codes = [code for code, _ in SOVEREIGN_STATES.values()]
    if len(SOVEREIGN_STATES) != 195 or len(set(codes)) != 195:
        sys.exit("Expected 195 sovereign states with unique codes.")

    countries = sorted(
        ({"code": code, "name": display or name, "population": rows[name]}
         for name, (code, display) in SOVEREIGN_STATES.items()),
        key=lambda c: c["name"],
    )
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(
        json.dumps(
            {
                "source": SOURCE_URL,
                "capturedOn": captured_on,
                "populationYear": int(year.group(1)) if year else None,
                "countries": countries,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    dropped = sorted(set(rows) - set(SOVEREIGN_STATES))
    print(f"Wrote {len(countries)} countries to {OUTPUT}")
    print(f"Dropped {len(dropped)} dependencies and territories: {', '.join(dropped)}")


if __name__ == "__main__":
    main()
