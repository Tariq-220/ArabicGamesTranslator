# SPDX-License-Identifier: AGPL-3.0-only
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace
from server import Engine, load_sentencepiece, ensure_quickmt_files

class PathTests(unittest.TestCase):
    def test_arabic_cache_never_reaches_native_path_loaders(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / 'اسم عربي' / 'نموذج'
            folder.mkdir(parents=True)
            for name in ['config.json','model.bin','src.spm.model','tgt.spm.model','shared_vocabulary.json']:
                (folder / name).write_bytes(b'model-data')
            loaded = []
            class Processor:
                def LoadFromSerializedProto(self, data): loaded.append(data); return True
                def encode(self, text, out_type=str): return [text]
                def decode(self, text): return 'مرحبا'
            def runtime(path, **args):
                self.assertTrue(path.isascii())
                self.assertIn('model.bin', args['files'])
                self.assertTrue(all(isinstance(v,bytes) for v in args['files'].values()))
                return SimpleNamespace(translate_batch=lambda *a,**k:[SimpleNamespace(hypotheses=[['ok']])])
            def no_download(*a,**k): self.fail('Valid cached files must not download again')
            modules={'sentencepiece':SimpleNamespace(SentencePieceProcessor=Processor),
                'huggingface_hub':SimpleNamespace(snapshot_download=lambda *a,**k:str(folder),hf_hub_download=no_download),
                'ctranslate2':SimpleNamespace(Translator=runtime),
                'blingfire':SimpleNamespace(text_to_sentences=lambda s:s)}
            with patch.dict('sys.modules',modules):
                self.assertEqual('مرحبا',Engine('quickmt',offline=True).translate('Hello.'))
            self.assertEqual([b'model-data',b'model-data'],loaded)

    def test_only_missing_file_is_repaired(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp)
            for name in ['config.json','model.bin','src.spm.model']: (folder/name).write_bytes(b'valid')
            calls=[]
            def download(repo,filename,**kw):
                calls.append(filename);p=folder/filename;p.write_bytes(b'fixed');return str(p)
            ensure_quickmt_files(folder,False,download)
            self.assertEqual(['tgt.spm.model'],calls)
            self.assertEqual(b'valid',(folder/'model.bin').read_bytes())

    def test_incomplete_offline_cache_is_explicit(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(FileNotFoundError,'config.json'):
                ensure_quickmt_files(Path(tmp),True,None)

    def test_real_sentencepiece_reads_unicode_path_as_bytes(self):
        import sentencepiece as sp
        writer=io.BytesIO()
        sp.SentencePieceTrainer.train(sentence_iterator=iter(['hello world this is a test']*30),
            model_writer=writer,vocab_size=24,hard_vocab_limit=False,minloglevel=2)
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'مستخدم عربي';path.mkdir();path=path/'src.spm.model';path.write_bytes(writer.getvalue())
            processor=load_sentencepiece(sp,path)
            self.assertEqual('hello world',processor.decode(processor.encode('hello world')))

if __name__=='__main__': unittest.main()
