all: XmlMarkdown.dll xmlmd.exe musexlat.exe

XMLMD_SOURCES=XmlMarkdown.cs SmartyPants.cs XhtmlWriter.cs

XmlMarkdown.dll: $(XMLMD_SOURCES)
	gmcs -t:library -out:$@ -r:System.Web $(XMLMD_SOURCES)

xmlmd.exe: $(XMLMD_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(XMLMD_SOURCES)

MUSEXLAT_SOURCES=MuseTranslate.cs

musexlat.exe: $(MUSEXLAT_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(MUSEXLAT_SOURCES)

INSTDIR=$(HOME)/Sites/johnw

install: XmlMarkdown.dll
	cvs commit -m changes
	cp XmlMarkdown.dll $(INSTDIR)/bin

clean:
	rm *.exe *.mdb
