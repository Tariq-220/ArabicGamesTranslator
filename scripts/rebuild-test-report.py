"""Expose bounded test failures as check annotations when artifact downloads are unavailable."""
from pathlib import Path
import xml.etree.ElementTree as ET
for path in Path('android').glob('**/build/test-results/**/TEST-*.xml'):
    root=ET.parse(path).getroot()
    print(f"{root.get('name')}: tests={root.get('tests')} failures={root.get('failures')} errors={root.get('errors')}")
    for case in root.findall('testcase'):
        for failure in list(case.findall('failure'))+list(case.findall('error')):
            text=((failure.get('message') or '')+'\n'+(failure.text or ''))[:5000]
            escaped=text.replace('%','%25').replace('\r','%0D').replace('\n','%0A')
            print(f"::error title={case.get('classname')}.{case.get('name')}::{escaped}")
