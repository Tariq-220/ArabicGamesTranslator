import importlib.util,json,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('collector',Path(__file__).with_name('collect-community-feedback.py'));c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
class CollectionTests(unittest.TestCase):
 def issue(self,n=1,user=1,proposal='أسرع!',game='GTA6'):
  return {'number':n,'user':{'id':user},'labels':[{'name':'translation-review'}],'body':'### بيانات البلاغ\n\n'+json.dumps({'game':game,'engine':'QuickMT','source':'Haul ass!','arabic':'اسحب المؤخرة','proposed':proposal},ensure_ascii=False)}
 def test_duplicates_across_engines_and_aliases_group_without_autoapproval(self):
  a=self.issue();b=self.issue(2,2,game='Grand Theft Auto VI');b['body']=b['body'].replace('QuickMT','DeepL');out=c.collect([a,b],{'entries':[]});g=out['groups'][0];self.assertEqual(len(out['groups']),1);self.assertEqual(g['contributors'],2);self.assertEqual(g['engines'],['QuickMT','DeepL']);self.assertFalse(g['reviewed']);self.assertEqual(g['status'],'pending')
 def test_same_account_is_not_an_extra_quality_vote(self):self.assertEqual(c.collect([self.issue(),self.issue(2)],{'entries':[]})['groups'][0]['contributors'],1)
 def test_conflicting_proposals_are_never_majority_approved(self):
  groups=c.collect([self.issue(),self.issue(2,2,proposal='اهرب!')],{'entries':[]})['groups'];self.assertTrue(all(g['status']=='conflict' and not g['reviewed'] for g in groups))
 def test_sensitive_content_is_rejected_without_copying_it(self):
  row=self.issue();row['body']+='AIza'+'x'*30;out=c.collect([row],{'entries':[]});self.assertEqual(out['groups'],[]);self.assertEqual(out['rejected_reports'],[1]);self.assertNotIn('AIza',json.dumps(out))
 def test_current_phone_clipboard_and_separate_proposal_are_supported(self):
  row=self.issue();row['body']='### بيانات البلاغ\nاللعبة: GTA6\nالمحرك: QuickMT\nEnglish:\nHaul ass!\nالعربية الحالية:\nاسحب المؤخرة\nالتصحيح المقترح: غير محدد\n\n### الترجمة العربية الصحيحة المقترحة\nأسرع!\n';self.assertEqual(c.parse_report(row)['proposed'],'أسرع!')
 def test_existing_reviewed_translation_is_marked_published(self):
  approved={'entries':[{'game':'Grand Theft Auto VI','source':'Haul ass!','arabic':'أسرع!','reviewed':True}]};g=c.collect([self.issue()],approved)['groups'][0];self.assertEqual(g['status'],'published');self.assertFalse(g['reviewed'])
 def test_context_without_a_proposal_is_not_promoted_to_translation(self):
  row=self.issue(proposal='');row['body']+='\n### التصحيح المقترح والسياق\nيتكلم مع صاحبه';self.assertEqual(c.collect([row],{'entries':[]})['groups'][0]['status'],'needs_correction')
 def test_unchanged_windows_editor_is_not_a_proposed_correction(self):
  row=self.issue(proposal='اسحب المؤخرة');self.assertEqual(c.collect([row],{'entries':[]})['groups'][0]['status'],'needs_correction')
if __name__=='__main__':unittest.main()
