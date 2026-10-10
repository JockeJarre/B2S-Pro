const fs = require('fs');
const path = require('path');
const modules = path.join(__dirname,'node/node_modules');
const {createWorker} = require(path.join(modules,'tesseract.js'));
const sharp = require(path.join(modules,'sharp'));
(async()=>{
  const worker=await createWorker('eng',1,{langPath:__dirname,gzip:false,cacheMethod:'none'});
  try {
    await worker.setParameters({tessedit_pageseg_mode:process.argv[4]||'11',tessedit_char_whitelist:'0123456789'});
    const {data}=await worker.recognize(process.argv[2],{}, {blocks:true,text:true});
    let symbols=[];
    for(const block of data.blocks||[]) for(const para of block.paragraphs||[]) for(const line of para.lines||[]) for(const word of line.words||[]) for(const s of word.symbols||[]) {
      if(/^[0-9]$/.test(s.text)) symbols.push({digit:Number(s.text),confidence:s.confidence,...s.bbox});
    }
    const rejected=[];
    if(process.argv[4]!=='10' && symbols.length) {
      // Whole-page OCR locates glyphs; isolated OCR determines each glyph's value.
      // Reel artwork must never be assigned from its position in a row/column.
      const maxHeight=Math.max(...symbols.map(s=>s.y1-s.y0));
      const candidates=symbols.filter(s=>s.y1-s.y0>=maxHeight*0.5 && s.x1-s.x0 < (s.y1-s.y0)*0.95);
      symbols=[];
      await worker.setParameters({tessedit_pageseg_mode:'10',tessedit_char_whitelist:''});
      for(const s of candidates) {
        const glyph=await sharp(process.argv[2]).extract({left:s.x0,top:s.y0,width:s.x1-s.x0,height:s.y1-s.y0}).resize({height:160}).extend({top:40,bottom:40,left:40,right:40,background:'#ffffff'}).png().toBuffer();
        const {data:single}=await worker.recognize(glyph,{}, {text:true});
        const raw=single.text.trim();
        const text=({O:'0',o:'0',I:'1',l:'1','|':'1',S:'5',s:'5',B:'8',A:'4','/':'7'}[raw]||raw);
        if(/^[0-9]$/.test(text)) symbols.push({...s,digit:Number(text),confidence:single.confidence,raw,review:raw!==text});
        else rejected.push({...s,raw});
      }
    }
    fs.writeFileSync(process.argv[3],JSON.stringify({text:data.text,symbols,rejected},null,2));
  } finally {await worker.terminate();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
