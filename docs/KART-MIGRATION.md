# Kart UFN — Unity

O kart usa a simulação C# em `Assets/UFN/Core/Kart*.cs` e a apresentação Unity em `Assets/UFN/Presentation/Kart*.cs`. A simulação avança a 60 Hz e não depende de Godot nem de objetos Unity.

## Conteúdo preservado

- Cinco pistas: Circuito do Campus, Volta da Vila Belga, Centro de Santa Maria, Desafio Universitário e UFN / Andradas. Os vértices de controle e a interpolação Catmull–Rom em 12 subdivisões são os da versão 0.6.
- Corrida rápida com oito corredores; um ou dois humanos. Contra o relógio com um corredor, sem caixas, itens ou adversários. Campeonato com quatro etapas, o mesmo elenco e a pontuação 15 / 12 / 10 / 8 / 6 / 4 / 2 / 1.
- Três voltas e vinte checkpoints ordenados por volta. Passar pela linha de chegada sem completar os checkpoints não concede a volta.
- Aceleração, freio, marcha à ré, drift com miniturbo, redução fora do asfalto, barreiras, colisões, sentido errado, rampas na pista universitária, itens dianteiros/traseiros e proteção temporária após impacto.
- CPUs dirigem com aceleração, freio e direção, usando a mesma integração de movimento do humano. Não são deslocadas ao longo de uma trajetória pré-gravada.
- Todos os quatorze poderes próprios: halter, grito, capivara, raízes, drone, bola com curva, handebol, turbo de motor, parábola, aliada, pacote de dados, espuma, tijolos e patada lateral.
- Em tela dividida, cada câmera acompanha o respectivo humano. A divisão pode ser vertical ou horizontal.
- A classificação é congelada quando todos os humanos concluem a prova. CPUs que ainda não chegaram mantêm a posição por progresso validado, sem receber um tempo de chegada inventado.
- O resgate manual retorna ao último checkpoint validado, com um segundo de penalidade e três segundos de intervalo. Não concede voltas nem pontos.
- O campeonato pode ser retomado entre etapas; sair durante uma prova reinicia essa etapa sem alterar os pontos anteriores. A importação da 0.6 preserva etapa, pontos e humanos. Como o formato antigo não guardava a semente do elenco CPU, esses adversários são reconstruídos; novos campeonatos Unity preservam também esse elenco.
- Recordes `tempo_0` a `tempo_4` do perfil antigo são importados de segundos para milissegundos. Apenas o modo contra o relógio atualiza esses recordes de pista, evitando misturar tempos de corridas com itens.

## Personagens e referências

As quinze imagens fornecidas em 13 de outubro de 2026 estão preservadas, sem alterações, em `docs/kart-references`. `sources.json` associa cada arquivo ao original e registra SHA-256. Não são texturas de um modelo 3D extraído automaticamente.

Os pilotos são modelos geométricos próprios, articulados e sentados, definidos em `KartModel.cs`. O catálogo `KartAppearance` especifica cabelo, roupa, óculos, barba, detalhes e cores do kart conforme essas referências. As mãos acompanham o volante, os pés permanecem sobre os pedais e o tronco acompanha a direção e a aceleração.

**Diogo Careca é exclusivamente a skin 1 de Diogo (personagem 3).** Conserva identidade, poderes e física. O elenco continua tendo quatorze pilotos jogáveis; a skin não ocupa um novo personagem.

Esta apresentação 3D é estilizada e usa geometria procedural. Ela não reproduz literalmente a ilustração pixel a pixel, nem contém modelos esculpidos ou texturas pintadas à mão. As imagens originais ficam no projeto para futuras revisões artísticas.

## Verificação

`KartTests.cs` exercita corridas completas nas cinco pistas com dois humanos dirigidos por comandos de CPU, campeonato de quatro etapas, contra o relógio, cada poder, controles independentes e equivalência física entre as duas skins de Diogo. O teste rejeita movimentos descontínuos em condução normal, checkpoints falsos e alteração dos resultados depois da chegada. Também valida restauro do campeonato e rejeição de pontuações adulteradas. A execução focada concluída em 17/10/2026 passou em 134 verificações, sem falhas.

`UFN.Editor.KartCaptureTools.Capture` gera as quinze variantes em três ângulos, cinco vistas gerais das pistas e uma captura de tela dividida. A execução dessas capturas e sua revisão devem ser registradas no relatório da distribuição; o comando existir não constitui evidência de execução.

Os materiais do kart carregam os shaders próprios `UFNKartLit` e `UFNKartUnlit` da pasta `Resources`. Isso evita depender de shaders padrão removidos do executável quando não estão referenciados por materiais da cena.
