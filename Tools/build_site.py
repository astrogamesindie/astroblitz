# Builds the static site for GitHub Pages in ../Site from PlayConsole/*.md.
# Run: python Tools/build_site.py
import html, os, re, shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'PlayConsole')
OUT = os.path.join(ROOT, 'Site')

PLAY_URL = 'https://play.google.com/store/apps/details?id=com.astrocrawler.game'
APPSTORE_URL = 'https://apps.apple.com/app/id6824909243'
EMAIL = 'astrogamesindie@gmail.com'


def inline(text):
    text = html.escape(text, quote=False)
    text = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', text)
    text = re.sub(r'`(.+?)`', r'<code>\1</code>', text)
    text = re.sub(r'([\w.+-]+@[\w-]+\.[\w.]+)', r'<a href="mailto:\1">\1</a>', text)
    return text


def md_to_html(md):
    out, in_list = [], False
    for line in md.splitlines():
        line = line.rstrip()
        if line.startswith('- '):
            if not in_list:
                out.append('<ul>')
                in_list = True
            out.append('<li>%s</li>' % inline(line[2:]))
            continue
        if in_list:
            out.append('</ul>')
            in_list = False
        if not line:
            continue
        m = re.match(r'(#{1,3}) (.*)', line)
        if m:
            level = len(m.group(1))
            out.append('<h%d>%s</h%d>' % (level, inline(m.group(2)), level))
        else:
            out.append('<p>%s</p>' % inline(line))
    if in_list:
        out.append('</ul>')
    return '\n'.join(out)


HEAD = '''<!doctype html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<meta name="description" content="{desc}">
<link rel="icon" href="icon.png">
<link rel="stylesheet" href="style.css">
</head>
<body>
'''

FOOT = '''<footer>
  <a href="index.html">Astro Blitz</a> ·
  <a href="terms.html">Terms of Service</a> ·
  <a href="termos.html">Termos de Serviço</a> ·
  <a href="mailto:{email}">{email}</a>
  <p>© 2026 AstroGamesIdle</p>
</footer>
</body>
</html>
'''.format(email=EMAIL)

STORES = '''<div class="stores">
  <a class="store" href="{play}" target="_blank" rel="noopener">
    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 2.5v19l10.2-9.5L4 2.5zm11.6 8.2 2.8-2.6L6.3 1.4l9.3 9.3zm0 2.6-9.3 9.3 12.1-6.7-2.8-2.6zm4.3-3.8-2.6 2.5 2.6 2.5 2.6-1.4c.9-.5.9-1.8 0-2.3l-2.6-1.3z"/></svg>
    <span><small>{play_small}</small>Google Play</span>
  </a>
  <a class="store" href="{apple}" target="_blank" rel="noopener">
    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M16.4 12.6c0-2.6 2.1-3.8 2.2-3.9-1.2-1.8-3.1-2-3.7-2-1.6-.2-3.1.9-3.9.9-.8 0-2-.9-3.4-.9-1.7 0-3.3 1-4.2 2.6-1.8 3.1-.5 7.7 1.3 10.2.9 1.2 1.9 2.6 3.2 2.6 1.3-.1 1.8-.8 3.3-.8 1.6 0 2 .8 3.4.8 1.4 0 2.3-1.3 3.1-2.5 1-1.4 1.4-2.8 1.4-2.9 0 0-2.7-1-2.7-4.1zM13.9 5c.7-.9 1.2-2 1.1-3.2-1 0-2.3.7-3 1.6-.7.8-1.2 2-1.1 3.1 1.2.1 2.3-.6 3-1.5z"/></svg>
    <span><small>{apple_small}</small>App Store</span>
  </a>
</div>'''


def store_buttons(lang):
    if lang == 'pt':
        return STORES.format(play=PLAY_URL, apple=APPSTORE_URL, play_small='Disponível no', apple_small='Baixar na')
    return STORES.format(play=PLAY_URL, apple=APPSTORE_URL, play_small='Get it on', apple_small='Download on the')


def write(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


def doc_page(md_file, out_name, lang, title):
    md = open(os.path.join(SRC, md_file), encoding='utf-8').read()
    body = md_to_html(md)
    page = HEAD.format(lang=lang, title=title, desc=title)
    page += '<header class="bar"><a href="index.html" class="brand"><img src="icon.png" alt=""> Astro Blitz</a>'
    page += '<nav><a href="terms.html"%s>EN</a><a href="termos.html"%s>PT</a></nav></header>\n' % (
        ' class="on"' if lang == 'en' else '', ' class="on"' if lang == 'pt' else '')
    page += '<main class="doc">\n%s\n</main>\n' % body
    write(out_name, page + FOOT)


def index_page():
    page = HEAD.format(lang='en', title='Astro Blitz — Space Defense Roguelite',
                       desc='Astro Blitz: a fast space arena roguelite for Android and iOS.')
    page += '''<main class="home">
  <img class="banner" src="banner.png" alt="Astro Blitz — Space Defense Roguelite">
  <section class="intro">
    <img class="icon" src="icon.png" alt="Astro Blitz icon">
    <h1>Astro Blitz</h1>
    <p class="lead">Pilot a lone starship through hostile sectors, pick upgrade cards between waves and take down massive bosses.</p>
    <p class="lead pt">Pilote uma nave por setores hostis, escolha cartas de melhoria entre as ondas e derrote chefes gigantes.</p>
    {stores}
  </section>
  <section class="links">
    <a href="terms.html">Terms of Service</a>
    <a href="termos.html">Termos de Serviço</a>
    <a href="mailto:{email}">Support / Suporte</a>
  </section>
</main>
'''.format(stores=store_buttons('en'), email=EMAIL)
    write('index.html', page + FOOT)


CSS = '''*{box-sizing:border-box}
:root{--bg:#070b18;--panel:#0f1630;--ink:#e8f6ff;--dim:#9db3c8;--accent:#3ce4ff;--line:#1d2a4d}
html{background:var(--bg)}
body{margin:0;min-height:100vh;color:var(--ink);font:16px/1.65 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;
background:radial-gradient(1200px 600px at 15% -10%,#2a1450 0,transparent 60%),radial-gradient(900px 500px at 100% 30%,#0b3a4a 0,transparent 60%),var(--bg)}
a{color:var(--accent)}
.home{max-width:960px;margin:0 auto;padding:24px 16px 40px;text-align:center}
.banner{width:100%;border-radius:16px;display:block;box-shadow:0 20px 60px rgba(0,0,0,.5),0 0 40px rgba(60,228,255,.15)}
.intro{margin-top:-56px;position:relative}
.icon{width:112px;height:112px;border-radius:24px;border:3px solid var(--bg);box-shadow:0 0 30px rgba(60,228,255,.35)}
h1{margin:12px 0 4px;font-size:40px;letter-spacing:2px;text-transform:uppercase}
.lead{max-width:620px;margin:6px auto;color:var(--dim)}
.lead.pt{font-size:14px;opacity:.85}
.stores{display:flex;gap:14px;justify-content:center;flex-wrap:wrap;margin:26px 0 8px}
.store{display:flex;align-items:center;gap:12px;min-width:200px;padding:10px 20px;border-radius:12px;background:#000;color:#fff;
text-decoration:none;border:1px solid #3a4560;transition:transform .15s,border-color .15s}
.store:hover{transform:translateY(-2px);border-color:var(--accent)}
.store svg{width:28px;height:28px;fill:#fff;flex:none}
.store span{display:flex;flex-direction:column;text-align:left;font-size:20px;font-weight:600;line-height:1.1}
.store small{font-size:11px;font-weight:400;opacity:.8}
.links{display:flex;gap:10px 22px;justify-content:center;flex-wrap:wrap;margin-top:28px}
.links a{color:var(--ink);text-decoration:none;border-bottom:1px solid var(--line);padding-bottom:2px}
.links a:hover{color:var(--accent);border-color:var(--accent)}
.bar{display:flex;align-items:center;justify-content:space-between;max-width:820px;margin:0 auto;padding:16px}
.brand{display:flex;align-items:center;gap:10px;color:var(--ink);text-decoration:none;font-weight:700;letter-spacing:1px}
.brand img{width:36px;height:36px;border-radius:8px}
nav a{margin-left:8px;padding:4px 10px;border-radius:8px;border:1px solid var(--line);color:var(--dim);text-decoration:none;font-size:14px}
nav a.on{color:var(--bg);background:var(--accent);border-color:var(--accent)}
.doc{max-width:820px;margin:0 auto 32px;padding:28px 24px;background:rgba(15,22,48,.85);border:1px solid var(--line);border-radius:16px}
.doc h1{font-size:30px;text-transform:none;letter-spacing:0;margin-top:0}
.doc h2{font-size:20px;margin:28px 0 6px;color:var(--accent)}
.doc li{margin:4px 0}
code{background:#0a1022;padding:1px 6px;border-radius:6px}
footer{text-align:center;color:var(--dim);font-size:13px;padding:24px 16px 32px}
footer a{color:var(--dim)}
@media (max-width:600px){h1{font-size:30px}.intro{margin-top:-40px}.icon{width:88px;height:88px}.doc{margin:0 12px 24px;padding:20px 16px}.store{min-width:0;width:100%;max-width:280px}}
'''

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    shutil.copy(os.path.join(SRC, 'icone_512.png'), os.path.join(OUT, 'icon.png'))
    shutil.copy(os.path.join(SRC, 'recurso_grafico_1024x500.png'), os.path.join(OUT, 'banner.png'))
    write('style.css', CSS)
    write('.nojekyll', '')
    index_page()
    doc_page('terms_of_service_en.md', 'terms.html', 'en', 'Terms of Service — Astro Blitz')
    doc_page('termos_de_servico_pt.md', 'termos.html', 'pt', 'Termos de Serviço — Astro Blitz')
    print('built', sorted(os.listdir(OUT)))
