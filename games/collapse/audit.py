"""Read-only four-way audit, requiring the original game and the pinned Workshop item."""
import argparse
from collections import defaultdict
from decimal import Decimal
from pathlib import Path
import re
import sys
import unicodedata

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import collapse

RUSSIAN_OBJECTS = {'system_text':663, 'credits_text':664, 'gui_text':667,
    'modifiesnames_text':668, 'other_text':672, 'personages':675, 'event_text':676}
FORMAT = re.compile(r'\{\s*(\d+)\s*(?::([^{}]*))?\}')
TAG = re.compile(r'<(/?)(color|b|i|size)(?:=[^<>]+)?\s*>',re.I)
CJK = re.compile(r'[\u3400-\u9fff]')

# Short source abbreviations can name both a country and a faction.
# Only treat an unambiguous long name in both source languages as a review lead.
ENTITIES = (
    (r'\b(?:China|People.s Republic of China)\b', r'Кита[йеяю]|КНР', r'中国|中华|中方|北京|大陆|俄中|中俄|中美|美中'),
    (r'\bHong Kong\b', r'Гонконг', r'香港|港澳'),
    (r'\bMacau\b|\bMacao\b', r'Макао', r'澳门|港澳'),
    (r'\bRussian Federation\b', r'Российск\w* Федерац', r'俄|莫斯科'),
    (r'\bUnited States\b', r'Соедин[её]нн\w* Штат|США', r'美|华盛顿'),
    (r'\bCommonwealth of Independent States\b', r'Содружеств\w* Независим\w* Государств|СНГ', r'独联体|独立国家联合体'),
)

def missing_entity(en, ru, zh):
    target = compact(zh)
    return any(re.search(e, en, re.I) and re.search(r, ru, re.I) and not re.search(z, target)
               for e, r, z in ENTITIES)

def read_tables(game, workshop):
    source = game/collapse.ASSET
    pristine = source.read_bytes()
    if collapse.digest(pristine) != collapse.PRISTINE:
        pristine = (game/'.collapse-zh-24949409'/'resources.assets.original').read_bytes()
    target, _ = collapse.build(pristine, workshop)
    for key,(pid,offset,count,_) in collapse.TABLES.items():
        rid = RUSSIAN_OBJECTS[key]
        rus_id, rus_name, rus = collapse.object_data(pristine,offset+(rid-pid)*24)
        collapse.require((rus_id,rus_name)==(rid,key),'俄文对象索引错误：'+key)
        eng = collapse.object_data(pristine,offset)[2]
        final = collapse.object_data(target,offset)[2]
        base = (workshop/(key+'.txt')).read_bytes()
        columns = [x.decode('utf-8-sig').splitlines() for x in (eng,rus,base,final)]
        collapse.require(all(len(x)==count for x in columns),'四栏行数不符：'+key)
        yield key, list(zip(*columns))

def compact(s):
    return re.sub(r'\s','',s)

def plain(s):
    return FORMAT.sub('',TAG.sub('',s))

def numbers(s):
    s = unicodedata.normalize('NFKC',plain(s)).replace('−','-').replace('–','-')
    return {Decimal(n.replace(',','.')) for n in re.findall(r'(?<!\d)\d+(?:[.,]\d+)?',s)}

def signed_values(s):
    # An explicit action cost / gain is more informative than the presence
    # of the number alone. Preserve placeholder IDs and ignore prose hyphens.
    visible=unicodedata.normalize('NFKC',TAG.sub('',s)).replace('−','-')
    pairs=re.findall(r'([+-])\s*(\{\d+(?::[^{}]+)?\}|\d+(?:[.,]\d+)?)',visible)
    return {(sign, value if value.startswith('{') else Decimal(value.replace(',','.')))
            for sign,value in pairs}

def field_set(s):
    return {(int(i),f) for i,f in FORMAT.findall(s) if int(i)>0}

def rich_ok(s):
    stack = []
    for close,tag in TAG.findall(s.lower()):
        if close:
            if not stack or stack.pop()!=tag: return False
        else:
            stack.append(tag)
    return not stack

def audit(tables):
    hard = defaultdict(list)
    candidates = defaultdict(list)
    counts = {}
    trait_labels = {}
    trait_buttons = {}
    trait_headings = {}
    for key,rows in tables:
        counts[key] = len(rows)
        groups = defaultdict(list)
        for line,(en,ru,base,zh) in enumerate(rows,1):
            ref=(key,line)
            if missing_entity(en, ru, zh):
                candidates['named_entity'].append(ref)
            # The same character trait appears in the ballot, the nomination
            # report's format argument and the officeholder's tooltip.
            if key == 'event_text':
                if 8325 <= line <= 8344:
                    trait_labels[line-8325] = compact(zh)
                elif 8298 <= line <= 8307:
                    trait_buttons[line-8298] = compact(zh)
                elif 8312 <= line <= 8321:
                    trait_buttons[line-8302] = compact(zh)
            elif key == 'gui_text' and 760 <= line <= 779:
                trait_headings[line-760] = compact(plain(zh.split('|',1)[0]))
            family = collapse.family_dialog_row(key, line)
            if bool(ru.strip() if family else base.strip()) != bool(zh.strip()):
                hard['blank_shift'].append(ref)
            if re.search(r'[{}]',FORMAT.sub('',zh)):
                hard['broken_format'].append(ref)
            # These rows are arguments to an outer String.Format, which does
            # not expand fields inside its arguments. Players see the braces.
            if key == 'event_text' and line in (2831, 5364, 5379, 5394, 5403, 5414, 5426, 5436):
                if FORMAT.search(zh):
                    hard['unexpanded_event_hint'].append(ref)
            # The Chinese coalition formatter embeds these two fragments in its
            # outer format string: {0} is a regional change, {5} is the party grant.
            if key == 'system_text' and line == 1552:
                # MinistryController.Repaint passes this label directly to TextMesh.
                # Its English {0} was intended as a line break but is never formatted.
                if FORMAT.search(zh):
                    hard['unexpanded_ministry_action'].append(ref)
            elif key == 'event_text' and line == 4987:
                # Event 529 passes the party, a line break, and the foreign
                # ministry's own reaction. The Russian template uses all three.
                if FORMAT.findall(zh) != [('1',''), ('0',''), ('2','')]:
                    hard['iraq_statement_fields'].append(ref)
            elif key == 'gui_text' and line in (1761, 1762):
                expected = [('5',''), ('0','')] if line == 1761 else [('0','')]
                if FORMAT.findall(zh) != expected:
                    hard['coalition_format_fields'].append(ref)
            else:
                if '{0}' in zh and '{0}' not in en and '{0}' not in ru:
                    candidates['extra_zero_field'].append(ref)
                if field_set(ru if family else en)!=field_set(zh):
                    hard['format_ids_or_specifiers'].append(ref)
            if not rich_ok(zh):
                hard['rich_text_nesting'].append(ref)
            if en.strip() and not zh.strip():
                candidates['source_but_empty'].append(ref)
            if not CJK.search(zh) and re.search(r'[A-Za-zА-Яа-я]{4,}',compact(plain(zh))):
                candidates['all_foreign'].append(ref)
            if (numbers(en) & numbers(ru)) - numbers(compact(zh)):
                candidates['missing_number'].append(ref)
            source_signed=signed_values(en) & signed_values(ru)
            target_signed=signed_values(compact(zh))
            if any((('+' if sign=='-' else '-'),value) in target_signed
                   and (sign,value) not in target_signed for sign,value in source_signed):
                candidates['opposite_sign'].append(ref)
            if (re.search(r'\b(?:less|greater|below|above|more than|no more|no less)\b',en,re.I)
                    and not re.search('高|低|多|少|大|小|至|超|满|达|不足|[<>≥≤]',compact(zh))):
                candidates['threshold'].append(ref)
            if (len(en)>200 and len(compact(plain(zh))) < len(plain(en))*0.12):
                candidates['short_translation'].append(ref)
            if zh.strip():
                groups[compact(zh)].append((line,compact(en).lower(),compact(ru).lower()))
        for entries in groups.values():
            if len({en for _,en,_ in entries})>1 and len({ru for _,_,ru in entries})>1:
                candidates['shared_translation'].extend((key,n) for n,_,_ in entries)
    for idx, name in trait_labels.items():
        role = '总统' if idx < 10 else '总理'
        if trait_headings.get(idx) != f'{role}（{name}）':
            hard['candidate_trait_heading'].append(('gui_text',760+idx))
        if trait_buttons.get(idx) != f'提名候选人（{name}）':
            hard['candidate_trait_ballot'].append(('event_text',(8298 if idx<10 else 8302)+idx))
    return counts,hard,candidates

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game','--game-dir',type=Path)
    p.add_argument('--workshop',type=Path)
    p.add_argument('--list',action='store_true',help='列出候选项编号。')
    p.add_argument('--show',nargs=2,metavar=('TABLE','ROW'),help='本机显示指定行的四栏内容。')
    args=p.parse_args()
    game=args.game or collapse.locate()
    workshop=args.workshop or collapse.find_workshop(game)
    collapse.require(workshop is not None, '请先获取工坊底包，或用 --workshop 指定其目录。')
    tables=dict(read_tables(game,workshop))
    if args.show:
        key,s=args.show; n=int(s)
        collapse.require(key in tables and 1<=n<=len(tables[key]),'行号或表名错误')
        for language,t in zip(('EN','RU','工坊','最终'),tables[key][n-1]):
            print(language,t)
    counts,hard,candidates=audit(tables.items())
    print('四栏逐行对齐：',counts,'共',sum(counts.values()))
    for name,findings in [('结构错误',hard),('语义复核线索',candidates)]:
        print(name,{k:len(v) for k,v in sorted(findings.items())})
        if args.list or name=='结构错误':
            for k,values in sorted(findings.items()):
                print(k,' '.join(f'{table}/{n}' for table,n in values))
    return int(any(hard.values()))

if __name__=='__main__':
    try:
        sys.exit(main())
    except (OSError,ValueError,KeyError) as exc:
        sys.exit(str(exc))
