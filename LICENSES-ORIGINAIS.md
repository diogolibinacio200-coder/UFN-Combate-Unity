# Créditos, procedência e licenças

## Autoria e contato

**Criado e desenvolvido por Diogo Liberalesso Inácio.**

**Feito por Diogo Liberalesso Inácio. Para contato ou feedback, fale no Instagram [@diogok_i](https://www.instagram.com/diogok_i/) ou pelo e-mail [diogolib.inacio200@gmail.com](mailto:diogolib.inacio200@gmail.com)!**

UFN Combate — Edição Campus, versão 0.5.0, 2026. Desenvolvimento assistido por IA, a partir da direção e dos materiais fornecidos pelo autor.

## Base técnica

A implementação parte do projeto [Nexus Kombat](https://github.com/diogolibinacio200-coder/nexus-kombat), da mesma conta do autor, com adaptação e ampliação do combate, interface, elenco, progressão, áudio e modo de kart.

## Materiais do projeto

- **Personagens:** sprites fornecidos pelo autor em setembro de 2026. O processamento remove fundos, recorta poses, organiza atlas e prepara retratos. [Correspondência de arquivos e personagens](docs/REFERENCIAS.md).
- **Animações:** sequências de poses dos atlas, combinadas com interpolação, deslocamentos e efeitos em Godot. Não representam dezenas de novos desenhos independentes por animação.
- **Arenas:** oito imagens produzidas com a ferramenta de geração de imagens integrada. Quatro utilizam referências locais pesquisadas e quatro são composições temáticas. As fotografias originais não integram as arenas distribuídas; os respectivos autores e links constam em [REFERENCIAS.md](docs/REFERENCIAS.md).
- **Urso de Fernando:** atlas de oito poses gerado para o jogo e usado com efeitos e posicionamento próprios.
- **Aliada loira de Vitor do Mal:** arte original gerada com a ferramenta ImageGen para esta atualização, em [assets/sprites/vitor_ally_blonde/atlas.png](assets/sprites/vitor_ally_blonde/atlas.png). As poses são recortadas e ancoradas por `scripts/ally_art.gd` para as invocações, a coreografia da Ultimate e o poder de kart. É uma personagem de apoio exclusiva; não reutiliza a identidade de Maria nem cria um novo slot jogável. Maria permanece no elenco e conserva seus recursos próprios.
- **Referências de pilotos no kart:** 15 imagens fornecidas pelo autor em 5 de outubro de 2026, preservadas em [assets/kart/references](assets/kart/references/) e relacionadas em [manifest.json](assets/kart/references/manifest.json). Correspondem aos 14 pilotos e à aparência Diogo Careca. Elas orientam os modelos procedurais, roupas, cabelos, cores e detalhes dos veículos; sua inclusão não declara uma licença pública de reutilização.
- **Kart e efeitos:** geometria, veículos, itens e efeitos construídos no código do projeto.
- **Pista UFN / Andradas:** geometria e sinalização estilizadas produzidas no código, com o mapa fornecido pelo autor e fontes públicas usados como referências geográficas. A imagem do mapa fornecido e fotografias de Street View não são distribuídas como texturas da pista. [Fontes e adaptações](docs/PISTA-UFN.md).
- **Música e efeitos sonoros:** síntese produzida para a base do projeto e reproduzível por `tools/generate_audio.py`.
- **Narrador:** síntese em português do Brasil com a voz **Microsoft Maria Desktop**, instalada no computador de desenvolvimento. Gerador: `tools/generate_announcer_pt.ps1`. Não é gravação de um ator.
- **Identidade UFN:** nome, marca e logotipo institucional pertencem aos respectivos titulares. O arquivo do logo foi obtido no portal da UFN. A presença da identidade universitária no jogo não declara aprovação ou patrocínio institucional.

Este documento registra a procedência dos materiais; não concede uma licença geral de reutilização das imagens fornecidas, personagens, marca institucional ou voz. Nenhuma licença única para todo o código e toda a arte foi definida nesta entrega. As licenças dos componentes de terceiros abaixo continuam aplicáveis aos respectivos componentes.

## Godot Engine

**Godot Engine 4.7.2 — licença MIT.**

Copyright (c) 2014–present Godot Engine contributors. Copyright (c) 2007–2014 Juan Linietsky, Ariel Manzur.

O texto incluído está em [docs/GODOT-LICENSE.txt](docs/GODOT-LICENSE.txt). Informações e avisos dos componentes da engine estão na [página oficial de licenças da Godot](https://godotengine.org/license/) e no editor em **Ajuda → Sobre → Licenças de terceiros**.

## Fonte Rajdhani

Copyright (c) 2014, Indian Type Foundry.

**SIL Open Font License 1.1.** Texto integral em [assets/fonts/OFL.txt](assets/fonts/OFL.txt).

## Distribuição

A distribuição Windows usa **UFNCombate.exe** e **UFNCombate.pck**, que devem permanecer juntos. A rotina de empacotamento mantém os dados de UFN Combate no PCK separado e pode usar o executável oficial **Godot 4.7.2** renomeado. A procedência e a verificação do executável efetivamente entregue ficam registradas em [docs/QA.md](docs/QA.md). Uma assinatura da engine não constitui assinatura do conteúdo, da autoria ou da distribuição do jogo por Diogo Liberalesso Inácio. Os avisos da engine e da fonte devem acompanhar o pacote.

As referências a outros jogos descrevem inspiração de gênero e estrutura; o projeto não inclui assets extraídos de Mortal Kombat ou Street Fighter.
