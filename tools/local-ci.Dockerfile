FROM ubuntu:22.04
ARG DEBIAN_FRONTEND=noninteractive
RUN apt-get -o Acquire::Retries=3 update && apt-get -o Acquire::Retries=3 install -y ca-certificates \
    && sed -i 's|http://|https://|g' /etc/apt/sources.list && rm -f /etc/apt/apt.conf.d/docker-clean
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked apt-get -o Acquire::Retries=3 update && apt-get -o Acquire::Retries=3 -o Acquire::https::Timeout=45 install -y --no-install-recommends build-essential curl ca-certificates libssl-dev zlib1g-dev libbz2-dev libreadline-dev libsqlite3-dev libffi-dev liblzma-dev \
    rpm desktop-file-utils xvfb xauth weston fonts-noto-core fonts-noto-cjk fonts-dejavu-core poppler-utils \
    libegl1 libgl1 libdbus-1-3 libxkbcommon-x11-0 libxcb-cursor0 libxcb-icccm4 libxcb-image0 libxcb-keysyms1 libxcb-render-util0 libxcb-shape0
RUN curl -fsSL https://www.python.org/ftp/python/3.11.9/Python-3.11.9.tgz | tar xz -C /tmp \
    && cd /tmp/Python-3.11.9 && ./configure --prefix=/opt/python --enable-shared --with-ensurepip=install \
    && make -j4 && make install && rm -rf /tmp/Python-3.11.9
ENV PATH=/opt/python/bin:$PATH LD_LIBRARY_PATH=/opt/python/lib
RUN ln -s /opt/python/bin/python3 /opt/python/bin/python
WORKDIR /work
