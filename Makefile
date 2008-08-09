all: OpenMarkdown.dll markdown.exe musexlat.exe

XMLMD_SOURCES=OpenMarkdown.cs SmartyPants.cs XhtmlWriter.cs

OpenMarkdown.dll: $(XMLMD_SOURCES)
	gmcs -t:library -out:$@ -r:System.Web $(XMLMD_SOURCES)

markdown.exe: $(XMLMD_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(XMLMD_SOURCES)

MUSEXLAT_SOURCES=MuseTranslate.cs

musexlat.exe: $(MUSEXLAT_SOURCES)
	gmcs -debug -out:$@ -r:System.Web $(MUSEXLAT_SOURCES)

INSTDIR=$(HOME)/Sites/johnw

install: OpenMarkdown.dll
	svn commit -m changes
	cp OpenMarkdown.dll $(INSTDIR)/bin

clean:
	rm *.exe *.mdb
