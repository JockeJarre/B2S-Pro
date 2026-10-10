import sys, json
import pypdfium2 as pdfium
doc=pdfium.PdfDocument(sys.argv[1])
page=doc[int(sys.argv[2])-1]
text=page.get_textpage()
width,height=page.get_size()
scale_x,scale_y=int(sys.argv[3])/width,int(sys.argv[4])/height
symbols=[]
for index in range(text.count_chars()):
    digit=text.get_text_range(index,1)
    if len(digit)!=1 or digit not in '0123456789': continue
    left,bottom,right,top=text.get_charbox(index)
    if right<=left or top<=bottom: continue
    symbols.append(dict(digit=int(digit),confidence=100,raw=digit,review=False,x0=round(left*scale_x),y0=round((height-top)*scale_y),x1=round(right*scale_x),y1=round((height-bottom)*scale_y)))
json.dump(dict(symbols=symbols),open(sys.argv[5],'w'),indent=2)
